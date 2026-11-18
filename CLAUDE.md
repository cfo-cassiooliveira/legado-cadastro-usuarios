# Legado — Cadastro de Usuários

> Índice de docs: `docs/INDEX.md` · Referência de logs/LGPD: `docs/reference.md`.
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
- Ordem e comandos com o Keycloak/Cadastro: `./scripts/identity-network.sh` → `./scripts/internal-credentials.sh` →
  `docker compose -f docker-compose.yml -f docker-compose.identity.yml [-f docker-compose.graylog.yml] up -d --build`
  (README, "Ordem para subir com o Keycloak e o Cadastro"). O Keycloak lê `LEGADO_INTERNAL_KEYCLOAK_KEY` daqui como
  `KC_LEGADO_SIGNING_KEY` e monta `secrets/internal/ca.crt`; o ADR-0005 está **aceito**. Os chamadores só podem usar
  TLS com a CA deste Legado (nunca `http://`, nunca outra CA); mudar o contrato (rota, assinatura, permissões) exige
  nova versão e ajuste nos testes do Keycloak e do Cadastro, que usam os vetores de `Internal/hmac-test-vectors.json`.
- Imagens Docker fixadas por **tag + digest**: atualize os dois juntos.
- Testes .NET: xUnit v3 no **Microsoft.Testing.Platform** (`global.json`) e cobertura via
  `Microsoft.Testing.Extensions.CodeCoverage`.
- Health checks: `/health/live`, `/health/ready` (SQL Server) e `/health` (detalhado), só na rede interna.
- Logs: Graylog do projeto `../graylog` via `docker-compose.graylog.yml` (ADR-0003). API → GELF TCP; demais
  containers → Docker GELF driver em `udp://127.0.0.1:12201` com `tag` = `service_name`. Todo log novo usa
  `[LoggerMessage]`/message template e **nunca** dado pessoal ou segredo (os testes em
  `tests/Legado.Api.IntegrationTests/Logging/` inspecionam os eventos reais).
- Detalhes de pseudonimização de logs (LGPD), níveis de log, `AppLogger` e Serilog: **`docs/reference.md`** (as regras
  que não podem regredir lá descritas continuam valendo). Resumo: nunca dado pessoal/segredo em log; a API pseudonimiza
  na origem (`PseudonymizationEnricher`; `PersonalDataFields` = listas `LGPD_*` do `../graylog`); sem chave válida a API não sobe.

## Comandos

```bash
dotnet build Legado.slnx --nologo -v q -warnaserror        # silencioso; se falhar, repita sem -v q
dotnet test --solution Legado.slnx --no-logo -v q          # integração exige Docker (Testcontainers)
dotnet tool restore && dotnet ef migrations add <Nome> \
  --project legado-database/src/Legado.Database --startup-project legado-database/src/Legado.Database
cd legado-frontend && npm ci --silent --no-audit --no-fund && npx ng test --watch=false && npx ng lint && npm run lint:styles && npx ng build
./scripts/e2e.sh                                           # E2E com a stack de pé (Playwright em container)
./scripts/sonar-analysis.sh                                # SonarQube local (../sonarqube) + Quality Gate (ADR-0004)
cp .env.example .env && docker compose up -d --build       # http://localhost:4200
```

## Decisões e particularidades

- Migrations ficam em `legado-database` (assembly `Legado.Database`) e são aplicadas **só pelo migrator**,
  nunca no startup da API. A API usa o login `legacy_app` (sem DDL).
- O contrato de erros devolve **códigos** (`postal_code.not_found`) e o Angular traduz via `$localize`.
- O CSP do nginx bloqueia scripts inline, por isso o `inlineCritical` do build Angular fica desligado.
