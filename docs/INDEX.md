# Índice de docs (Legado)
ADRs (todos Aceitos), em `docs/adr/`:
- 0001 Autenticação própria legada: `0001-autenticacao-propria-legada.md`
- 0002 Validação de endereço ViaCEP+IBGE: `0002-validacao-endereco-viacep-ibge.md`
- 0003 Logs no Graylog: `0003-logs-centralizados-graylog.md`
- 0004 Quality Gate SonarQube local: `0004-quality-gate-sonarqube-local.md`
- 0005 API interna de migração (TLS+HMAC): `0005-api-interna-de-migracao-para-keycloak.md`

Specs: não há `docs/specs/`.

Comandos (detalhes no `CLAUDE.md`, seção Comandos):
`dotnet build Legado.slnx --nologo -v q -warnaserror` · `dotnet test --solution Legado.slnx --no-logo -v q`
· `./scripts/e2e.sh` · `./scripts/sonar-analysis.sh` · `docker compose up -d --build`

Onde procurar:
- Regras, stack, comandos: `CLAUDE.md`
- Logs, LGPD, pseudonimização, níveis: `docs/reference.md`
- Subir com Keycloak/Cadastro: `README.md`; rede de dados e esquema import: `docs/reference.md`
