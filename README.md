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

Se uma variável obrigatória faltar, o `docker compose` para com a mensagem `defina <VARIÁVEL> no .env`.
A API também não sobe sem uma `JWT_SIGNING_KEY` válida, nem com `SEED_ENABLED=true` sem `SEED_DEFAULT_PASSWORD`.

> Para desenvolvimento fora do Docker, use `dotnet user-secrets` no projeto `Legado.Api`
> (`ConnectionStrings:LegadoDb`, `Jwt:SigningKey`), nunca o `appsettings.json`.

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
requisição, e-mail, nome, celular e IP. Isso é garantido por teste de integração que inspeciona os eventos reais;
o Graylog ainda mascara e-mail e CPF como defesa em profundidade.

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
dotnet test --solution Legado.slnx            # integração sobe SQL Server real via Testcontainers
cd legado-frontend && npm ci && npx ng test --watch=false && npx ng lint
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
