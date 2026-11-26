#!/bin/bash
# Aplica as migrations (EF bundle) e garante o login de aplicação. Idempotente: pode rodar a cada "compose up".
set -euo pipefail

: "${DB_HOST:?DB_HOST is required}"
: "${DB_NAME:?DB_NAME is required}"
: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"
: "${APP_DB_USER:?APP_DB_USER is required}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"

echo "Applying migrations to ${DB_NAME}..."
/migrator/efbundle --connection "Server=${DB_HOST},1433;Database=${DB_NAME};User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True"

echo "Ensuring application login ${APP_DB_USER}..."
# SQLCMDPASSWORD evita expor a senha na linha de comando do processo.
SQLCMDPASSWORD="${MSSQL_SA_PASSWORD}" /opt/mssql-tools18/bin/sqlcmd \
  -S "${DB_HOST}" -U sa -C -b \
  -i /migrator/create-app-login.sql \
  -v AppLogin="${APP_DB_USER}" AppPassword="${APP_DB_PASSWORD}" DbName="${DB_NAME}"

# Esquema "import" do Cadastro: só quando as duas senhas estão no .env (o Legado sobe sozinho sem elas).
if [[ -n "${CADASTRO_MIGRATOR_PASSWORD:-}" && -n "${CADASTRO_APP_PASSWORD:-}" ]]; then
  echo "Ensuring import schema and Cadastro logins..."
  SQLCMDPASSWORD="${MSSQL_SA_PASSWORD}" /opt/mssql-tools18/bin/sqlcmd \
    -S "${DB_HOST}" -U sa -C -b \
    -i /migrator/create-import-schema.sql \
    -v DbName="${DB_NAME}" LegacyAppLogin="${APP_DB_USER}" \
       MigratorLogin="cadastro_migrator" MigratorPassword="${CADASTRO_MIGRATOR_PASSWORD}" \
       CadastroAppLogin="cadastro_app" CadastroAppPassword="${CADASTRO_APP_PASSWORD}"
else
  echo "CADASTRO_MIGRATOR_PASSWORD/CADASTRO_APP_PASSWORD not set; skipping import schema."
fi

echo "Database ready."
