# ADR-0005: API interna de migração (TLS + HMAC), cadastro fechado e independência do Legado

- **Status**: Proposto
- **Data**: 2026-11-13
- **Decisores**: Cassio Oliveira (responsável técnico)
- **Artigos da Constituição relacionados**: Art. III.1, Art. III.4, Art. IV.4, Art. IV.6, Art. VIII.2, Art. IX.1

## Contexto

O Keycloak passa a migrar os usuários do Legado sob demanda (ADR-0006 do keycloak) e o novo sistema `cadastro` precisa
saber se um login/e-mail já existe no Legado. O banco do Legado não pode ser compartilhado (Art. III.4). Login e senha
trafegam do Keycloak para o Legado e precisam estar protegidos. E, por ser legado, **o Legado precisa continuar
independente**: desligar o Keycloak ou o Cadastro não pode afetá-lo.

## Decisão

1. **Direção da dependência**: só Keycloak/Cadastro → Legado. O Legado não chama, não valida tokens e não verifica a
   saúde de nenhum sistema novo. As credenciais da integração são **emitidas pelo Legado** e entregues aos chamadores.
2. **Endpoints internos** em `/api/v1/internal/` (verificação de credenciais, consulta, marcação de migração e checagem
   de existência), servidos **só** num endpoint Kestrel HTTPS `:8443`, que só existe com `InternalApi:Enabled=true`
   (override `docker-compose.identity.yml`). Na porta pública (8080, atrás do nginx) essas rotas não existem, e o nginx
   ainda devolve 404 para `/api/v1/internal/`.
3. **Transporte**: TLS 1.3 com certificado de uma CA local gerada pelo `scripts/internal-credentials.sh` (validade de
   1 ano, SAN `legado-api`). Os chamadores confiam só nessa CA.
4. **Autenticação por HMAC-SHA256**, chave de 256 bits por chamador (`keycloak`, `cadastro`), até duas ativas por
   chamador para troca sem parada. Cabeçalho:
   `X-Legado-Signature: keyId=<id>, t=<unix s>, n=<nonce>, v1=<base64(HMAC(chave, t\nnonce\nMÉTODO\nrota\nhex(SHA-256(corpo))))>`.
   Recusa (401 genérico) por `keyId` desconhecido, `|agora − t| > 60 s`, assinatura inválida (comparação em tempo
   constante) ou reenvio (nonce já visto para a chave nos últimos 120 s). O nonce (16 bytes aleatórios em base64url, um por
   chamada) deixa passar retentativas legítimas idênticas no mesmo segundo, que uma chave de reenvio baseada na
   própria assinatura recusaria. Permissões: `keycloak` → verificação, consulta e
   marcação; `cadastro` → checagem de existência (403 fora disso).
5. A verificação usa o Identity com lockout e verifica um hash fictício para usuário inexistente (tempo equalizado).
6. Colunas `MigratedAt` e `KeycloakUserId` (expand). Conta migrada: login com senha correta devolve
   `auth.account_migrated`; a marcação renova o `SecurityStamp` (sessões abertas caem).
7. O autocadastro do Legado continua aberto (decisão revista na implementação: o Legado só muda no login de conta
   migrada). O login de conta migrada com a senha certa devolve `newSystemUrl` (`Migration:NewSystemUrl`, só http(s)
   absoluto, validado na subida), e a tela redireciona para o Cadastro em 5 s, com "Ficar nesta página" para cancelar.
8. Migração de mão única (Legado → Keycloak): o Legado nunca recebe nem reserva contas criadas no Cadastro. O Keycloak
   é o mandatório; login criado no Legado que já exista no Keycloak não migra (risco aceito, spec 001 RN-09).

**Vetores de teste** (usados pelos testes do Legado, do Keycloak e do Cadastro) são gerados na T10 e versionados em
`legado-backend/tests/.../Internal/hmac-test-vectors.json`, com chave fictícia.

## Alternativas consideradas

| Alternativa | Prós | Contras | Motivo da rejeição |
|---|---|---|---|
| Keycloak lê o banco do Legado | Sem mudança no Legado | Banco compartilhado | Viola o Art. III.4 |
| JWT client credentials do Keycloak | Sem segredo novo | Legado passa a depender do Keycloak (emissor, JWKS) | Legado precisa ser independente |
| Chave fixa no cabeçalho (`X-Api-Key`) | Muito simples | Capturada, é reutilizável; não protege o corpo | HMAC cobre integridade e reenvio |
| Login e senha em Base64 | Simples | Codificação reversível, sem proteção | Falsa sensação de segurança (OWASP A04) |
| mTLS | Autentica o chamador no transporte | Certificado de cliente por chamador; mais operação | HMAC já autentica; pode evoluir depois |
| Corpo cifrado (JWE) além do TLS | Protege mesmo com TLS terminado no meio | Mais chaves e código | Sem intermediário entre os dois hoje; **evolução** se surgir gateway/mesh |
| Legado continua aceitando login de conta migrada | Sem mudança no login | Duas contas e duas senhas divergentes | Uma conta por pessoa |

## Consequências

- **Positivas**: o Legado continua dono do hash e do lockout, sobe sozinho e não conhece os sistemas novos; senha
  protegida no transporte; chamador autenticado, corpo íntegro e sem reenvio.
- **Negativas / riscos**: certificado e chaves para gerar, distribuir e renovar (script idempotente, alerta de
  validade); cache anti-reenvio em memória vale para uma instância só.
- **Ações decorrentes**: migration `AddUserMigrationColumns`; `scripts/internal-credentials.sh` e
  `scripts/identity-network.sh`; `docker-compose.identity.yml`; atualizar o `CLAUDE.md` do legado (regra "não pode
  regredir": o Legado não depende de Keycloak/Cadastro; rotas internas só na 8443).
