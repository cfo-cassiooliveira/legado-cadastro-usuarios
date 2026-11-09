# ADR-0001: Autenticação própria legada (exceção temporária ao Art. IV.2)

- **Status**: Aceito
- **Data**: 2026-11-08
- **Decisores**: Cassio Oliveira (responsável técnico)
- **Artigos da Constituição relacionados**: Art. IV.2, Art. IV.4, Art. VI.1, Governança

## Contexto

Precisamos de uma **base legada realista** para simular, em seguida, a migração da autenticação própria
para o **Keycloak**, levando os usuários já existentes **com a senha** (sem forçar reset).
A Constituição (Art. IV.2) proíbe autenticação caseira. Este projeto existe justamente para representar
esse "antes", portanto precisa violar a regra de forma controlada.

## Decisão

1. **Exceção restrita** a `C:\Workspace\legado`, somente em ambiente local/demo. Nenhum outro projeto
   pode usar este código como referência de autenticação.
2. Login usuário/senha com **ASP.NET Core Identity + EF Core** (SQL Server). O hash fica no formato
   **Identity V3: PBKDF2-HMAC-SHA512, 100.000 iterações, salt de 128 bits**, que o Keycloak aceita nativamente
   (`pbkdf2-sha512`) via importação de credenciais. Isso permite migrar sem reset de senha.
3. Sessão por **JWT HS256 emitido pela própria API**, com 15 minutos de vida, **sem refresh token** e guardado
   **somente em memória** no Angular (nunca em `localStorage`/`sessionStorage`). As claims já usam os nomes
   do Keycloak (`sub`, `preferred_username`, `role`), sem nome, e-mail ou outro dado pessoal (minimização, LGPD).
4. Controles mínimos mantidos: política de senha forte, bloqueio após 5 falhas (15 min), resposta genérica
   para credenciais inválidas, mitigação de enumeração por tempo, rate limit no login e no cadastro, e
   auditoria de login nos logs (sem dados pessoais).
5. **Proteção contra uso indevido do token** (revisão de segurança de 2026-11-09):
   - **Fingerprint (OWASP JWT Cheat Sheet, *token sidejacking*)**: no login, a API gera 256 bits aleatórios,
     grava-os no cookie `__Host-legado-fgp` (`HttpOnly; Secure; SameSite=Strict; Path=/`) e coloca no token
     só o SHA-256 (`fgp`). Todo request precisa do token **e** do cookie. Um token vazado (XSS, log, DevTools,
     extensão) sozinho é recusado, e o JavaScript não consegue ler o cookie.
   - **Revogação no servidor**: o token carrega o SHA-256 do `SecurityStamp` do Identity (`sstamp`), conferido
     em toda requisição. O `POST /api/v1/auth/logout` gera um novo carimbo e invalida **todos** os tokens do
     usuário (logout em todos os dispositivos). Conta bloqueada perde o acesso na hora.
   - Validação estrita: só HS256 (bloqueia `alg: none`), com emissor, público, assinatura e expiração obrigatórios.
   - Respostas da API com `Cache-Control: no-store` e sem header `Server`.
   - Frontend com CSP sem `unsafe-inline` (nonce por requisição) e **Trusted Types** contra XSS.
   - Custo: uma consulta leve (por chave primária) ao banco a cada requisição autenticada.
6. **Riscos aceitos** (demonstração): o cadastro informa "login/e-mail já cadastrado" (enumeração de contas pelo
   autocadastro), e o bloqueio por tentativas permite travar de propósito uma conta conhecida.
7. O Graylog fica **fora do compose** (pesado para a demo). Os logs são JSON estruturados no console, com sink
   GELF habilitável por `Graylog__Host`.

## Alternativas consideradas

| Alternativa | Prós | Contras | Motivo da rejeição |
|---|---|---|---|
| Tabela própria + BCrypt | Mais "cara de legado" | Keycloak não lê BCrypt nativamente | Exigiria provider customizado só para a migração |
| Tabela própria + PBKDF2 | Colunas separadas de salt/iterações | Reimplementa o que o Identity já faz | Criptografia caseira (OWASP A04) |
| Cookie HttpOnly | Mais seguro, próximo do BFF | Menos representativo de legados com JWT | Escolha do responsável técnico: JWT |
| Já nascer no Keycloak | Conforme a Constituição | Não há o que migrar | Contraria o objetivo do projeto |

## Consequências

- **Positivas**: a massa de usuários tem hash importável no Keycloak, e as claims e perfis (`Admin`, `User`)
  mapeiam direto para realm roles.
- **Negativas / riscos**: autenticação fora do padrão e sem MFA, com refresh de página encerrando a sessão
  e chave HS256 compartilhada.
- **Ações decorrentes**: (1) projeto de migração para Keycloak 26 + BFF; (2) importar usuários com
  `credentialData {"hashIterations":100000,"algorithm":"pbkdf2-sha512"}` e
  `secretData {"value":<subkey base64>,"salt":<salt base64>}`, extraídos do blob V3 de `AspNetUsers.PasswordHash`;
  (3) descontinuar este ADR quando o legado for desligado.
