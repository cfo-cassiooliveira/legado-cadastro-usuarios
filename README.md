# Legado — Cadastro de Usuários

Sistema **legado proposital** de cadastro de usuários com login próprio (ASP.NET Core Identity + EF Core).
Serve de base para simular a **migração dos usuários existentes para o Keycloak**, levando a senha
(hash PBKDF2-SHA512 importável). Veja o [ADR-0001](docs/adr/0001-autenticacao-propria-legada.md).

## Estrutura

| Pasta | O que é |
|---|---|
| [`legado-database/`](legado-database/) | Banco legado: migrations EF Core (`Legado.Database`), migrator (EF bundle + `sqlcmd`) e script do login de aplicação |
| [`legado-backend/`](legado-backend/) | Web API .NET 10 em Clean Architecture (`Domain`, `Application`, `Infrastructure`, `Api`) + testes |
| [`legado-frontend/`](legado-frontend/) | Angular 22: login, cadastro em 4 passos, perfil e listagem administrativa, servido por nginx |
| [`docs/adr/`](docs/adr/) | Decisões de arquitetura |

```
navegador ──► web (nginx :4200) ──/api──► api (.NET :8080, rede interna) ──► sqlserver (:1433 só localhost)
                                              └──► ViaCEP (CEP) / IBGE (cidades)
migrator: aplica migrations + cria login legacy_app (roda e sai antes da api subir)
```

## Como subir

Pré-requisito: Docker Desktop.

```bash
cp .env.example .env      # preencha as senhas, a chave JWT e a senha da massa de demonstração
./scripts/log-pseudonymization-key.sh   # chave dos tokens de log no .env (a mesma do ../cadastro; idempotente)
docker compose up -d --build
```

Acesse **http://localhost:4200**. Na primeira subida (com `SEED_ENABLED=true`), a API cria 11 usuários fictícios.
Todos usam a senha `SEED_DEFAULT_PASSWORD` do `.env`:

| Login | Perfil |
|---|---|
| `admin` | Admin + User (vê a listagem de usuários) |
| `ana.souza`, `bruno.lima`, `carla.mendes`, … | User |

## Variáveis de ambiente (`.env`)

O `docker compose` lê os segredos do arquivo `.env` na raiz. O **`.env` nunca é versionado**: ele está
no `.gitignore` e no `.dockerignore`, portanto não vai para o git nem para as imagens. O repositório só traz
o modelo [`.env.example`](.env.example), sem valores, com instruções para gerar cada um.

| Variável | Obrigatória | Para que serve | Formato / como gerar |
|---|---|---|---|
| `MSSQL_SA_PASSWORD` | Sim | Senha do `sa` do SQL Server (container do banco e migrator) | 8+ caracteres com maiúscula, minúscula, número e símbolo; sem aspas. Ex.: `echo "Sa$(openssl rand -hex 12)#1"` |
| `APP_DB_PASSWORD` | Sim | Senha do login `legacy_app`, usado pela API (sem DDL) | Mesmas regras; diferente da senha do `sa` |
| `JWT_SIGNING_KEY` | Sim | Chave HS256 que assina os tokens de login | 32+ caracteres aleatórios. Ex.: `openssl rand -base64 48` |
| `SEED_ENABLED` | Não (padrão `false`) | Cria `admin` + 10 usuários fictícios na primeira subida | `true` ou `false` |
| `SEED_DEFAULT_PASSWORD` | Se `SEED_ENABLED=true` | Senha de todos os usuários de demonstração | 8+ caracteres com maiúscula, minúscula, número e símbolo |
| `API_ENVIRONMENT` | Não (padrão `Production`) | Ambiente ASP.NET Core da API | `Production` ou `Development` (publica o OpenAPI na rede interna) |
| `LEGADO_INTERNAL_KEYCLOAK_KEY` / `LEGADO_INTERNAL_CADASTRO_KEY` | Só com `docker-compose.identity.yml` | Chaves HMAC da API interna de migração, uma por chamador | Base64 de 32 bytes, geradas por `./scripts/internal-credentials.sh` |
| `LOG_PSEUDONYMIZATION_KEY_K1` / `LOG_PSEUDONYMIZATION_KEY_K2` | A do `CURRENT_KEY_ID` (a API não sobe sem ela) | Chave HMAC que gera os campos `*_token` dos logs (ADR-0003 do `../graylog`); a mesma do `../cadastro` no ambiente | Base64 de 32 bytes, gerada ou copiada do `../cadastro/.env` por `./scripts/log-pseudonymization-key.sh` |
| `LOG_PSEUDONYMIZATION_CURRENT_KEY_ID` | Não (padrão `k1`) | Id da chave que emite os tokens (rotação anual: `k2`...) | `k` + número |

Se uma variável obrigatória faltar, o `docker compose` para com a mensagem `defina <VARIÁVEL> no .env`.
A API também não sobe sem uma `JWT_SIGNING_KEY` válida, sem a chave de pseudonimização atual (Base64 de 256+ bits), nem com `SEED_ENABLED=true` sem `SEED_DEFAULT_PASSWORD`.

> Para desenvolvimento fora do Docker, use `dotnet user-secrets` no projeto `Legado.Api`
> (`ConnectionStrings:LegadoDb`, `Jwt:SigningKey`, `Logging:Pseudonymization:Keys:k1`; o script acima grava esta última), nunca o `appsettings.json`.

## Funcionalidades

- **Cadastro em 4 passos**: dados pessoais → acesso (checklist da política de senha) → endereço → revisão.
- **Endereço validado**: o CEP é consultado na base dos Correios (ViaCEP), as cidades vêm do IBGE e o backend
  confere se o CEP pertence à cidade e a cidade à UF ([ADR-0002](docs/adr/0002-validacao-endereco-viacep-ibge.md)).
- **Login** com bloqueio após 5 falhas, JWT de 15 min guardado só em memória e vinculado a um cookie HttpOnly.
- **Sair** revoga a sessão no servidor (o token deixa de valer na hora).
- **Perfil** do usuário logado e **listagem paginada** para administradores.
- Layout responsivo (desktop e celular).

## API (`/api/v1`)

| Método | Rota | Acesso |
|---|---|---|
| POST | `/auth/login` | anônimo (rate limit); devolve o token e grava o cookie de fingerprint |
| POST | `/auth/logout` | autenticado; revoga **todos** os tokens do usuário no servidor |
| POST | `/users` | anônimo, autocadastro (rate limit) |
| GET | `/users/me` | autenticado |
| GET | `/users?page=&pageSize=` | Admin (`users:read`) |
| GET | `/addresses/postal-codes/{cep}` | anônimo (rate limit) |
| GET | `/addresses/states/{uf}/cities` | anônimo (rate limit + cache) |
| POST | `/client-logs` | anônimo (rate limit); eventos de log do navegador, validados e saneados |

Erros seguem o **Problem Details (RFC 9457)** com códigos estáveis em `errors` (ex.: `{"email": ["email.taken"]}`).

## Segurança do acesso

Chamar a API autenticada exige **duas coisas juntas**: o header `Authorization: Bearer <token>` e o cookie
`__Host-legado-fgp`, ambos emitidos no login.

| Proteção | Como funciona | O que impede |
|---|---|---|
| Fingerprint do token | Cookie `HttpOnly; Secure; SameSite=Strict`; o token guarda só o SHA-256 dele | Usar um token vazado (XSS, log, DevTools, extensão) em outro lugar |
| Token só em memória | Nunca em `localStorage`/`sessionStorage`; recarregar a página encerra a sessão | Roubo do token persistido no navegador |
| Revogação no servidor | Token traz o hash do `SecurityStamp`; o logout gera um novo carimbo | Reuso do token depois do "Sair", em qualquer dispositivo |
| Bloqueio imediato | Conta bloqueada (5 falhas) perde acesso mesmo com token válido | Sessões abertas de uma conta sob ataque |
| Validação estrita do JWT | Só HS256, com emissor, público, assinatura e expiração obrigatórios | `alg: none`, adulteração do payload (ex.: virar Admin) |
| Minimização de dados | Token sem nome, e-mail ou celular; API com `Cache-Control: no-store` | Vazamento de dado pessoal por token ou cache |
| CSP + Trusted Types | Sem `unsafe-inline` (nonce por requisição), só scripts da própria origem | Injeção de script (XSS), que seria o vetor de roubo de token |
| Autorização | Fallback "negar por padrão"; `/users` só para Admin; `/me` usa só o `sub` do token | Acesso anônimo, escalada de perfil, IDOR |

Cada linha tem teste automatizado em `legado-backend/tests/Legado.Api.IntegrationTests/Security/`.
Os mesmos ataques também foram reproduzidos no navegador real (Edge, via Playwright).
Riscos aceitos e decisão completa: [ADR-0001](docs/adr/0001-autenticacao-propria-legada.md).

## Logs centralizados (Graylog)

Os logs de todos os serviços podem ir para o Graylog do projeto [`../graylog`](../graylog), que é opcional.
Decisão completa: [ADR-0003](docs/adr/0003-logs-centralizados-graylog.md).

```bash
# 1) Suba o Graylog (uma vez):     cd ../graylog && ./scripts/generate-env.sh && docker compose up -d --build
# 2) Suba o legado com o override:
docker compose -f docker-compose.yml -f docker-compose.graylog.yml up -d --build
```

Abra **http://localhost:9000** (usuário `admin`, senha em `../graylog/.env`) e use os streams:

| Stream | Origem | O que tem |
|---|---|---|
| `Legado - API` | Serilog → GELF TCP | Todo log da API, com `trace_id`, `span_id`, `correlation_id`, `request_id`, `user_id`, `message_template`, endpoint, status e latência |
| `Legado - Web (nginx)` | Docker GELF (UDP) | Access log JSON → `http_method`, `http_path`, `http_status`, `http_request_time`, `http_upstream_time`... |
| `Legado - Infra` | Docker GELF (UDP) | SQL Server e migrator |
| `Legado - Erros` | todos | Só erros reais (5xx, exceções); avisos do stderr recebem o nível correto |
| `Legado - Segurança` | API | Login (sucesso, falha, bloqueio), logout, token recusado, rate limit |
| `Legado - Navegador` | Frontend → `POST /api/v1/client-logs` | Erros JavaScript, falhas e operações da tela (`ClientEvent`, `ClientSource`, `ClientStatus`, stack) |

Para seguir uma requisição de ponta a ponta, busque `correlation_id:<id>` (vem do navegador) ou `request_id:<id>`
(gerado no nginx e repassado à API). Alertas já provisionados: erros, erros no navegador, falhas de autenticação
em massa, rate limit, API lenta e 5xx no nginx.

### Níveis de log (regra do workspace)

| Situação | Nível | Exemplos no legado |
|---|---|---|
| Exceção capturada (qualquer `catch` que trata) | **Error** + exceção | ViaCEP/IBGE fora, requisição malformada, sonda de health falhando, erro JS (`ErrorHandler` global), falha de rede no navegador |
| Falha de negócio esperada | **Warning** | Senha errada, conta bloqueada, token recusado, rate limit, cadastro rejeitado, CEP inexistente |
| Falha prevista de banco (insert/update) | **Warning** + 409 | Login/e-mail duplicado em corrida (índice único), conflito de concorrência ao revogar sessão |
| Operação concluída | **Information** | Cadastro, login, logout |

No frontend, o `AppLogger` envia cada evento para a API (só código do evento, tela e status, nunca valores
digitados ou URLs). Os testes de backend e frontend verificam o nível de cada um desses cenários.

**O que nunca vai para os logs**: senha, token, fingerprint, cookie, `Authorization`, query string, corpo de
requisição, e-mail, nome, celular e IP. Isso é garantido por teste de integração que inspeciona os eventos reais.

**Dado pessoal aprovado em ADR** (CPF, CNPJ, telefone, login, e-mail) só vai como campo estruturado com o nome do tipo
(`{Cpf}`, `{Cnpj}`, `{Phone}`/`{MobilePhone}`, `{UserName}`, `{Email}`) e é **pseudonimizado na origem**
(ADR-0003 do `../graylog`): o campo sai com a máscara (`***.456.789-**`, `12.3**.***/***1-90`, `11*****4321`, `mar***`,
`ana***@gmail`) e ganha `<Campo>_token` (`k1-` + HMAC-SHA256 truncado), que permite achar todos os eventos do
titular com igualdade exata (e-mail e login geram o mesmo token). Vale também dentro de objetos destruturados,
dicionários e listas: o campo mascarado é gravado no Graylog como `Customer_Email` e o token vai no topo do evento
como `Customer_Email_token` (nunca dentro do objeto, para não aparecer no texto), fora da consulta padrão do Cadastro. O texto
(`message`/`full_message`) sai só com a máscara; valor fora do formato vira `***`, sem token. No texto dos escopos
(`Scope`, renderizado em claro pelo provider), só o valor com máscara válida é trocado pela máscara, e CPF/CNPJ/telefone fora do
formato com 8+ dígitos vira `***`; login/e-mail curto ou inválido (de qualquer tamanho, ex.: `@gmail.com`) não é trocado, para não corromper palavras
(o Graylog, como rede de segurança, o troca por `***`): por isso
dado pessoal nunca vai em template de escopo. Esses campos e textos não saem da API com o valor em claro (teste com servidor GELF TCP falso em
`tests/Legado.Api.IntegrationTests/Logging/PseudonymizedGelfTests.cs`). **Não é tratado na origem**: a exceção
(mensagem e stack trace seguem como estão, por isso nunca devem conter dado pessoal) e nomes de campo fora das listas;
o Graylog continua mascarando o que escapar, como defesa em profundidade.

**Logs do navegador** (`POST /api/v1/client-logs`): o texto vem de fora, sem campo estruturado. A API tira query
string e fragmento de `ClientSource` e troca, em `ClientMessage`, `ClientSource` e `ClientStack`, e-mail (também
`%40`), CNPJ formatado, CPF e celular com DDD reconhecidos pelo formato pela mesma máscara (`ana***@gmail`,
`***.456.789-**`, `11*****4321`; 11 dígitos sem pontuação viram CPF), sem token
(`Common/Pseudonymization/PersonalDataTextMasker.cs`, testes em `Logging/ClientLogEndpointTests.cs` e
`Logging/PersonalDataTextMaskerTests.cs`). Celular sem DDD não é reconhecido: o frontend nunca envia valor digitado.

Chave: `./scripts/log-pseudonymization-key.sh` grava `LOG_PSEUDONYMIZATION_KEY_K1` no `.env` e no user-secrets (copia
do `../cadastro/.env` quando já existe lá). Rotação anual:
`./scripts/log-pseudonymization-key.sh k2` (a mesma `k2` do `../cadastro`) e `LOG_PSEUDONYMIZATION_CURRENT_KEY_ID=k2`
no `.env`; o `docker-compose.yml` já mapeia `k1` e `k2` (vazia = ausente). O legado só emite com a chave atual, então a
`LOG_PSEUDONYMIZATION_KEY_K1` pode sair do `.env` logo após a troca; quem pesquisa (Cadastro) mantém a `k1` até o fim
da retenção (30 dias) e depois a destrói. Sem a chave do `CurrentKeyId` a API não sobe.

## Health checks (rede interna)

| Endpoint | Verifica |
|---|---|
| `/health/live` | Processo respondendo |
| `/health/ready` | SQL Server (falha → 503) |
| `/health` | JSON detalhado: SQL Server, ViaCEP, IBGE (falha → `Degraded`), memória, GC, threads, uptime, versão |

```bash
docker compose exec web wget -qO- http://api:8080/health
```

## Desenvolvimento e testes

```bash
dotnet build Legado.slnx -warnaserror
dotnet test --solution Legado.slnx            # unitários, arquitetura (ArchUnitNET) e integração (Testcontainers)
cd legado-frontend && npm ci && npx ng test --watch=false && npx ng lint && npm run lint:styles
./scripts/e2e.sh                              # stack de pé: Playwright + axe em desktop, tablet e celular
```

### Qualidade (SonarQube)

A análise usa o SonarQube local do projeto [`../sonarqube`](../sonarqube) (http://localhost:9001), com o Quality Gate
"Workspace Way" (0 issues novas, cobertura ≥ 80%, duplicação ≤ 3% e 100% dos hotspots revisados, sempre em código novo).

```bash
./scripts/sonar-analysis.sh    # testes com cobertura (front e back), análise e espera do Quality Gate
```

O script lê o token em `../sonarqube/.tokens/legado-cadastro-usuarios.token` (gerado pelo provisionamento do
SonarQube) e termina com erro se o gate reprovar. O job `quality-gate` do CI roda o mesmo script, mas só quando os
segredos `SONAR_TOKEN` e `SONAR_HOST_URL` existem: os runners do GitHub não alcançam o SonarQube local
([ADR-0004](docs/adr/0004-quality-gate-sonarqube-local.md)).

## Dados para a migração ao Keycloak

- Tabela `AspNetUsers`: `PasswordHash` no formato Identity V3 (byte `0x01` + PRF SHA-512 + 100.000 iterações +
  salt 16 bytes + subkey 32 bytes, em base64).
- Perfis em `AspNetRoles`/`AspNetUserRoles` (`Admin`, `User`) mapeiam para realm roles.
- O banco fica acessível em `localhost:1433` (somente local) para o processo de migração.

### API interna de migração (opcional, [ADR-0005](docs/adr/0005-api-interna-de-migracao-para-keycloak.md))

O Keycloak (SPI) e o Cadastro consultam o Legado por uma API interna, desligada por padrão: sem ela o Legado sobe
e funciona sozinho.

```bash
./scripts/identity-network.sh       # rede Docker compartilhada workspace-identity (idempotente)
./scripts/internal-credentials.sh   # CA local, certificado legado-api e chaves HMAC no .env (idempotente)
docker compose -f docker-compose.yml -f docker-compose.identity.yml up -d --build
```

- Só em `https://legado-api:8443` (TLS 1.3, CA local em `secrets/internal/ca.crt`, que os chamadores copiam).
  Na porta pública as rotas `/api/v1/internal/*` não existem, e o nginx devolve 404 para elas.
- Cada chamada assina `X-Legado-Signature: keyId=<id>, t=<unix s>, n=<nonce>, v1=<HMAC>` (formato e vetores de
  teste em `legado-backend/tests/Legado.Api.IntegrationTests/Internal/hmac-test-vectors.json`).
- `secrets/` e `.env` não são versionados nem entram nas imagens.
- Conta já migrada (`MigratedAt` preenchido) que entra com a senha certa vê o aviso e é levada ao Cadastro em 5 s
  (`CADASTRO_URL`, padrão `http://localhost:4300/`). O autocadastro do Legado continua aberto.
  E2E desse modo: `E2E_MIGRATED_LOGIN=<conta migrada> E2E_MIGRATED_PASSWORD=<senha> ./scripts/e2e.sh`.
