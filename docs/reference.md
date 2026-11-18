# Referência — Logs, LGPD e pseudonimização (movido do CLAUDE.md, verbatim)

- A proibição acima não tem exceção implícita (Constituição, Art. IV.6). Só um dado pessoal **aprovado em ADR**
  vai ao log, e apenas como campo estruturado com o nome do tipo: `{Cpf}`, `{Cnpj}`, `{Phone}`/`{MobilePhone}`,
  `{UserName}`, `{Email}` no message template, **nunca** concatenado ou interpolado (destruturado `{@Obj}` é
  mascarado; o token vai no topo como `Obj_Campo_token`, fora da consulta padrão do Cadastro). A API **pseudonimiza na origem** (ADR-0003 do `../graylog`,
  `Common/Pseudonymization/`): o `PseudonymizationEnricher` troca o campo pela máscara do ADR-0002 (`***.456.789-**`,
  `11*****4321`, `mar***`, `ana***@gmail`) e soma `<Campo>_token` (`k1-` + HMAC-SHA256 truncado; e-mail e login dão
  o mesmo token), em qualquer nível (objeto, dicionário, lista); fora do formato vira `***`, sem token. O texto
  renderizado sai só com a máscara, nunca com o token. No `Scope` dos escopos só o valor com máscara válida é trocado pela
  máscara, e CPF/CNPJ/telefone fora do formato com 8+ dígitos vira `***`; login/e-mail curto ou inválido não é trocado
  (o Graylog o troca por `***`): nunca use dado pessoal em
  template de escopo. A exceção não é alterada, então nunca leve dado pessoal nela. O Graylog é só a rede de segurança (ADR-0002 do
  `../graylog`). Os nomes ficam em `PersonalDataFields`, iguais às listas `LGPD_*` do
  `../graylog/provisioning/provision.sh`: nome novo entra nos dois. Regras que **não podem regredir**: vetores do
  `plan.md` da spec 001 do graylog e nenhum valor em claro no GELF (`Logging/PseudonymizationTests.cs`,
  `Logging/PseudonymizedGelfTests.cs`); sem chave `Logging:Pseudonymization:Keys:<CurrentKeyId>` válida a API não
  sobe (chave vazia = ausente). Chave só em `.env`/user-secrets (`scripts/log-pseudonymization-key.sh [kN]`), a mesma
  do `../cadastro`; o compose mapeia k1 e k2 (rotação: k2 + `LOG_PSEUDONYMIZATION_CURRENT_KEY_ID=k2`, depois a K1 sai).
- Pontos reais que logam dado pessoal (só pelo enricher, máscara + `*_token`): login ok e falhas (inválido/inexistente/bloqueado/migrado) com `{UserName}` (o valor digitado também no usuário inexistente; fora do formato vira `***` sem token) e cadastro ok com `{UserName}`, `{Email}`, `{MobilePhone}`. Cadastro rejeitado loga só `{ErrorCodes}`; a resposta ao cliente segue genérica. Testes: `Logging/PersonalDataLogFlowTests.cs` (fluxo real, GELF e contrato dos nomes em `PersonalDataFields`).
- Níveis (regra em `observability.md`): exceção capturada = Error; falha de negócio ou falha prevista de banco
  (unicidade/concorrência) = Warning + resultado tratado; sucesso = Information. Frontend usa `AppLogger`
  (`POST /api/v1/client-logs`) e o `GlobalErrorHandler`; todo catch passa pelo logger. O endpoint tira query string e
  fragmento da origem e mascara pelo formato (`PersonalDataTextMasker`, máscara do `PersonalDataMasker`, sem token)
  e-mail, CNPJ, CPF e celular com DDD em `ClientMessage`/`ClientSource`/`ClientStack`; nunca envie valor de formulário.
- Serilog com `preserveStaticLogger: true` e request logging com o logger do host: nunca dependa do `Log.Logger`
  estático (um host descartado o encerrava e silenciava os logs dos outros).

## Autenticação própria e API interna de migração (movido do CLAUDE.md)

- Autenticação: **própria** (Identity + JWT HS256), por exceção do **ADR-0001**. Não use como referência.
  Regras que **não podem regredir**: token exige o cookie `__Host-legado-fgp` (fingerprint), sessão validada
  no servidor a cada request (`sstamp`/bloqueio), logout revoga no servidor, token sem dado pessoal, nada em
  `localStorage`/`sessionStorage`, CSP sem `unsafe-inline` e com Trusted Types (garantido por
  `Security/ContentSecurityPolicyTests`, que lê `security-headers.conf`). Os testes em `tests/Legado.Api.IntegrationTests/Security/` garantem o restante.
- API interna de migração (ADR-0005), regras que **não podem regredir**: desligada por padrão (o Legado nunca
  depende do Keycloak nem do Cadastro); só na porta HTTPS interna (8443), nunca na pública nem no nginx; assinatura
  HMAC com nonce, janela de 60 s e anti-reenvio; permissão por chamador (`keycloak`/`cadastro`); assinatura nunca em log; usuário inexistente gasta o mesmo tempo que senha errada. Cobertas por
  `tests/Legado.Api.IntegrationTests/Internal/`. Credenciais locais: `scripts/internal-credentials.sh` (`secrets/`).
- Ordem e comandos com o Keycloak/Cadastro: `./scripts/identity-network.sh` → `./scripts/internal-credentials.sh` →
  `docker compose -f docker-compose.yml -f docker-compose.identity.yml [-f docker-compose.graylog.yml] up -d --build`
  (README, "Ordem para subir com o Keycloak e o Cadastro"). O Keycloak lê `LEGADO_INTERNAL_KEYCLOAK_KEY` daqui como
  `KC_LEGADO_SIGNING_KEY` e monta `secrets/internal/ca.crt`; o ADR-0005 está **aceito**. Os chamadores só podem usar
  TLS com a CA deste Legado (nunca `http://`, nunca outra CA); mudar o contrato (rota, assinatura, permissões) exige
  nova versão e ajuste nos testes do Keycloak e do Cadastro, que usam os vetores de `Internal/hmac-test-vectors.json`.

## Glossário do domínio

| Termo de negócio | Nome no código | Definição |
|---|---|---|
| Nome completo | `FullName` | Nome e sobrenome, só letras |
| Login | `UserName` | 4–30 caracteres `[a-z0-9._-]`, minúsculo |
| Celular | `MobilePhone` | DDD válido + 9 dígitos iniciando em 9, salvo em E.164 |
| CEP | `PostalCode` | 8 dígitos, validado no ViaCEP |
| Município | `City` | Identificado pelo código IBGE |
| UF | `StateCode` | Sigla da unidade federativa |
