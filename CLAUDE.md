# Legado — Cadastro de Usuários

> Steering de **produto e estrutura** deste repositório. Herda a Constituição (`C:\Workspace\CLAUDE.md`)
> e o steering técnico (`C:\Workspace\.claude\rules\`). Pode restringir, nunca relaxar, as regras herdadas
> (a única exceção é a registrada no ADR-0001).

## Produto

- **Propósito**: sistema "legado" de cadastro de usuários, usado como **base para simular a migração** da
  autenticação própria (ASP.NET Core Identity) para o **Keycloak**, levando os usuários com a senha.
- **Usuários**: visitante (autocadastro), usuário (login e perfil) e administrador (listagem de usuários).
- **Bounded context**: Users, dono das tabelas `AspNet*` do banco `LegadoUsers`.
- **Fora de escopo**: edição de perfil, recuperação de senha, MFA, RabbitMQ/eventos.
- **Requisitos não funcionais**: guarda dados pessoais (nome, e-mail, celular, endereço), então vale a LGPD.
  Nada disso aparece em logs.

## Glossário do domínio

| Termo de negócio | Nome no código | Definição |
|---|---|---|
| Nome completo | `FullName` | Nome e sobrenome, só letras |
| Login | `UserName` | 4–30 caracteres `[a-z0-9._-]`, minúsculo |
| Celular | `MobilePhone` | DDD válido + 9 dígitos iniciando em 9, salvo em E.164 |
| CEP | `PostalCode` | 8 dígitos, validado no ViaCEP |
| Município | `City` | Identificado pelo código IBGE |
| UF | `StateCode` | Sigla da unidade federativa |

## Estrutura

```
legado-database/   Migrations EF (Legado.Database), migrator (EF bundle + sqlcmd), scripts SQL
legado-backend/    API .NET 10 (Domain, Application, Infrastructure, Api) + tests/
legado-frontend/   Angular 22 (standalone, signals, zoneless, Signal Forms, Vitest, Stylelint) + nginx
e2e/               Playwright + axe-core (desktop, tablet, celular) contra o docker compose (scripts/e2e.sh)
docs/adr/          ADR-0001 (auth legada), ADR-0002 (ViaCEP/IBGE), ADR-0005 (API interna de migração)
docker-compose.yml sqlserver -> migrator -> api -> web
```

## Stack deste projeto (diferenças do padrão)

- Banco: SQL Server 2025 em Docker, convenção PascalCase (tabelas padrão do Identity).
- Autenticação: **própria** (Identity + JWT HS256), por exceção do **ADR-0001**. Não use como referência.
  Regras que **não podem regredir**: token exige o cookie `__Host-legado-fgp` (fingerprint), sessão validada
  no servidor a cada request (`sstamp`/bloqueio), logout revoga no servidor, token sem dado pessoal, nada em
  `localStorage`/`sessionStorage`, CSP sem `unsafe-inline` e com Trusted Types. Os testes em
  `tests/Legado.Api.IntegrationTests/Security/` garantem isso.
- API interna de migração (ADR-0005), regras que **não podem regredir**: desligada por padrão (o Legado nunca
  depende do Keycloak nem do Cadastro); só na porta HTTPS interna (8443), nunca na pública nem no nginx; assinatura
  HMAC com nonce, janela de 60 s e anti-reenvio; permissão por chamador (`keycloak`/`cadastro`); login, senha e
  assinatura nunca em log; usuário inexistente gasta o mesmo tempo que senha errada. Cobertas por
  `tests/Legado.Api.IntegrationTests/Internal/`. Credenciais locais: `scripts/internal-credentials.sh` (`secrets/`).
- Imagens Docker fixadas por **tag + digest**: atualize os dois juntos.
- Testes .NET: xUnit v3 no **Microsoft.Testing.Platform** (`global.json`) e cobertura via
  `Microsoft.Testing.Extensions.CodeCoverage`.
- Health checks: `/health/live`, `/health/ready` (SQL Server) e `/health` (detalhado), só na rede interna.
- Logs: Graylog do projeto `../graylog` via `docker-compose.graylog.yml` (ADR-0003). API → GELF TCP; demais
  containers → Docker GELF driver em `udp://127.0.0.1:12201` com `tag` = `service_name`. Todo log novo usa
  `[LoggerMessage]`/message template e **nunca** dado pessoal ou segredo (os testes em
  `tests/Legado.Api.IntegrationTests/Logging/` inspecionam os eventos reais).
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
- Níveis (regra em `observability.md`): exceção capturada = Error; falha de negócio ou falha prevista de banco
  (unicidade/concorrência) = Warning + resultado tratado; sucesso = Information. Frontend usa `AppLogger`
  (`POST /api/v1/client-logs`) e o `GlobalErrorHandler`; todo catch passa pelo logger. O endpoint tira query string e
  fragmento da origem e mascara pelo formato (`PersonalDataTextMasker`, máscara do `PersonalDataMasker`, sem token)
  e-mail, CNPJ, CPF e celular com DDD em `ClientMessage`/`ClientSource`/`ClientStack`; nunca envie valor de formulário.
- Serilog com `preserveStaticLogger: true` e request logging com o logger do host: nunca dependa do `Log.Logger`
  estático (um host descartado o encerrava e silenciava os logs dos outros).

## Comandos

```bash
dotnet build Legado.slnx -warnaserror
dotnet test --solution Legado.slnx                         # integração exige Docker (Testcontainers)
dotnet tool restore && dotnet ef migrations add <Nome> \
  --project legado-database/src/Legado.Database --startup-project legado-database/src/Legado.Database
cd legado-frontend && npm ci && npx ng test --watch=false && npx ng lint && npm run lint:styles && npx ng build
./scripts/e2e.sh                                           # E2E com a stack de pé (Playwright em container)
./scripts/sonar-analysis.sh                                # SonarQube local (../sonarqube) + Quality Gate (ADR-0004)
cp .env.example .env && docker compose up -d --build       # http://localhost:4200
```

## Decisões e particularidades

- Migrations ficam em `legado-database` (assembly `Legado.Database`) e são aplicadas **só pelo migrator**,
  nunca no startup da API. A API usa o login `legacy_app` (sem DDL).
- O contrato de erros devolve **códigos** (`postal_code.not_found`) e o Angular traduz via `$localize`.
- O CSP do nginx bloqueia scripts inline, por isso o `inlineCritical` do build Angular fica desligado.
