# ADR-0003: Logs centralizados no Graylog (opcional por override do compose)

- **Status**: Aceito
- **Data**: 2026-11-09
- **Decisores**: Cassio Oliveira (responsável técnico)
- **Artigos da Constituição relacionados**: Art. VI.1, VI.2 e VI.3 (observabilidade), Art. IV.6 (dados pessoais fora dos logs)

## Contexto

A API já produzia logs estruturados no console (ADR-0001 deixava o Graylog fora do compose). O projeto
`C:\Workspace\graylog` passou a oferecer um Graylog provisionado em Docker. O legado precisa enviar a ele logs
ricos de **todos** os serviços, sem passar a depender dele para rodar.

## Decisão

1. **Override opcional** `docker-compose.graylog.yml`. Sem ele o legado roda como antes; com ele:
   - **API**: Serilog → GELF **TCP** para `graylog:12201` (rede Docker externa `observability`). Com TCP não se
     perdem mensagens como no UDP.
   - **web, sqlserver, migrator**: Docker logging driver **GELF UDP** em `127.0.0.1:12201` com `tag`, que vira
     `service_name` no pipeline do Graylog. Usamos `127.0.0.1`, não `localhost`: o daemon resolve `localhost`
     para IPv6 e a porta é publicada só em IPv4 (verificado no Docker Desktop).
2. **Conteúdo dos logs da API** (todo evento): `service_name`, `service_version`, `environment`, `trace_id`,
   `span_id`, `thread_id`, `correlation_id`, `user_id` (id técnico) e `message_template` (agrupa eventos iguais).
   O log de requisição traz método, rota, status, latência, endpoint, user agent, host e o `request_id` do nginx.
   Nível dinâmico: Error para 5xx ou exceção, Warning para mais de 1 s, Verbose (descartado) para health checks OK.
3. **nginx** com access log JSON (`time`, `request_id`, `correlation_id`, `method`, `path`, `status`, `bytes`,
   `request_time`, `upstream_time`, `user_agent`), repassando `X-Request-Id` à API para cruzar as duas fontes.
4. **Eventos de segurança** (stream e alertas no Graylog): login com sucesso, falha e bloqueio, logout com
   revogação, token recusado (fingerprint ou sessão) e rate limit excedido.
5. **Nunca nos logs**: senha, token, fingerprint, cookie, `Authorization`, query string, corpo de requisição,
   e-mail, nome, celular e IP do cliente. Isso é garantido por teste de integração que inspeciona os eventos
   reais. O Graylog ainda mascara e-mail e CPF como defesa em profundidade.
6. **Logs do navegador**: o frontend envia eventos (erros JS via `ErrorHandler` global, falhas capturadas e operações
   concluídas) para `POST /api/v1/client-logs`, que é anônimo porque falhas ocorrem antes do login. O endpoint
   valida nível e evento por lista fechada e padrão, limita tamanhos, remove caracteres de controle (log injection),
   mascara e-mail/CPF e tem rate limit próprio. Os eventos vão para o stream `Legado - Navegador`.
7. **Níveis** seguem a regra do workspace (`observability.md`): exceção capturada = Error; falha de negócio ou falha
   prevista de banco (unicidade no insert, concorrência no update) = Warning com resultado tratado (409); sucesso =
   Information.

## Alternativas consideradas

| Alternativa | Prós | Contras | Motivo da rejeição |
|---|---|---|---|
| Graylog obrigatório no compose do legado | Uma única subida | Acopla o legado à infraestrutura de logs | Override mantém o legado independente |
| Docker GELF driver também para a API | Configuração única | Perde a estrutura do Serilog (vira texto) | Sink GELF preserva os campos |
| OpenTelemetry Collector | Padrão de mercado, traces e métricas | Mais uma peça para operar | Fica para quando houver traces distribuídos |
| Logar IP do cliente | Investigação de abuso | Dado pessoal (LGPD) | Rota, `correlation_id` e `request_id` bastam |

## Consequências

- **Positivas**: busca por `correlation_id`, `request_id` ou `user_id` cruza navegador → nginx → API → caso de uso.
  Os alertas de erro, força bruta, rate limit, lentidão e 5xx funcionam sem configuração manual.
- **Negativas / riscos**: o UDP do Docker driver pode perder mensagens sob carga (aceitável para nginx e infra).
  Sem o Graylog de pé, o override falha ao subir porque a rede `observability` é externa.
- **Ações decorrentes**: novos serviços do legado ganham `tag` no override e, se necessário, um stream no
  `provision.sh` do projeto graylog.
