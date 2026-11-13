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
docs/adr/          ADR-0001 (auth legada), ADR-0002 (ViaCEP/IBGE)
docker-compose.yml sqlserver -> migrator -> api -> web
```

## Stack deste projeto (diferenças do padrão)

- Banco: SQL Server 2025 em Docker, convenção PascalCase (tabelas padrão do Identity).
- Autenticação: **própria** (Identity + JWT HS256), por exceção do **ADR-0001**. Não use como referência.
  Regras que **não podem regredir**: token exige o cookie `__Host-legado-fgp` (fingerprint), sessão validada
  no servidor a cada request (`sstamp`/bloqueio), logout revoga no servidor, token sem dado pessoal, nada em
  `localStorage`/`sessionStorage`, CSP sem `unsafe-inline` e com Trusted Types. Os testes em
  `tests/Legado.Api.IntegrationTests/Security/` garantem isso.
- Imagens Docker fixadas por **tag + digest**: atualize os dois juntos.
- Testes .NET: xUnit v3 no **Microsoft.Testing.Platform** (`global.json`) e cobertura via
  `Microsoft.Testing.Extensions.CodeCoverage`.
- Health checks: `/health/live`, `/health/ready` (SQL Server) e `/health` (detalhado), só na rede interna.
- Logs: Graylog do projeto `../graylog` via `docker-compose.graylog.yml` (ADR-0003). API → GELF TCP; demais
  containers → Docker GELF driver em `udp://127.0.0.1:12201` com `tag` = `service_name`. Todo log novo usa
  `[LoggerMessage]`/message template e **nunca** dado pessoal ou segredo (os testes em
  `tests/Legado.Api.IntegrationTests/Logging/` inspecionam os eventos reais).
- Níveis (regra em `observability.md`): exceção capturada = Error; falha de negócio ou falha prevista de banco
  (unicidade/concorrência) = Warning + resultado tratado; sucesso = Information. Frontend usa `AppLogger`
  (`POST /api/v1/client-logs`) e o `GlobalErrorHandler`; todo catch passa pelo logger.
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
