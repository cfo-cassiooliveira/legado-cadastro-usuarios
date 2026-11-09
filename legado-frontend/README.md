# legado-frontend

Frontend Angular 22 do sistema legado: login, cadastro em 4 passos, perfil e listagem administrativa.
Visão geral, segurança e variáveis de ambiente: [README da raiz](../README.md).

## Stack

Componentes standalone, signals, zoneless, `OnPush`, **Signal Forms**, `httpResource`, i18n com `$localize`
(idioma de origem `pt`) e testes com **Vitest**. Sem biblioteca de UI: o design system está em `src/styles.scss`.

## Estrutura (por feature)

```
src/app/
  auth/        login, AuthStore (token só em memória), interceptor Bearer, guards
  users/       cadastro (wizard), perfil, listagem de usuários, contrato da API
  addresses/   consulta de CEP e municípios (via API própria)
  core/        Problem Details → mensagens (i18n), correlation id
  shared/      máscaras, validadores, componentes de apoio
```

## Comandos

```bash
npm ci
npx ng build                      # build de produção (AOT)
npx ng test --watch=false         # testes (Vitest)
npx ng test --watch=false --coverage --coverage-include "src/app/**/*.ts" --coverage-exclude "src/app/**/*.spec.ts"
npx ng lint
npx prettier --check "src/**/*.{ts,html,scss}"
```

O app roda em produção atrás do nginx (`Dockerfile`, `nginx.conf`), que faz proxy de `/api` para a API.
Para subir tudo use `docker compose up -d --build` na raiz.

## Segurança no navegador

- Token de acesso **só em memória**; a API também exige o cookie `HttpOnly` de fingerprint, que o JavaScript não lê.
- CSP sem `unsafe-inline`: o nginx troca `__CSP_NONCE__` do `index.html` por um nonce aleatório a cada
  requisição (`security-headers.conf`), e o Angular usa esse nonce nos estilos dos componentes.
- **Trusted Types** obrigatório (`require-trusted-types-for 'script'`), com as políticas `angular` e `angular#bundler`.
- Não use `innerHTML`, `bypassSecurityTrust*` nem armazenamento do navegador para dados de sessão.
