#!/usr/bin/env bash
# Chave de pseudonimização dos logs (ADR-0003 do ../graylog), só para o ambiente local:
#   .env           LOG_PSEUDONYMIZATION_KEY_K<N> (HMAC, 256 bits em Base64), lida pelo docker compose
#   user-secrets   Logging:Pseudonymization:Keys:kN do Legado.Api, para "dotnet run" (se o dotnet estiver no PATH)
# A chave é a MESMA do ../cadastro no mesmo ambiente: se o ../cadastro/.env já tiver a mesma variável, ela
# é copiada; senão é gerada aqui (copie-a depois para o ../cadastro/.env).
# Idempotente: mantém a chave existente no .env. Nunca imprime o valor nem o passa em linha de comando.
# Uso: ./scripts/log-pseudonymization-key.sh [kN]   (padrão k1; na rotação anual: k2 e LOG_PSEUDONYMIZATION_CURRENT_KEY_ID=k2)
set -euo pipefail

cd "$(dirname "$0")/.."
ENV_FILE=".env"
CADASTRO_ENV="../cadastro/.env"
KEY_ID="${1:-k1}"
[[ "$KEY_ID" =~ ^k[1-9][0-9]*$ ]] || { echo "Id de chave inválido: use k1, k2..." >&2; exit 1; }
NAME="LOG_PSEUDONYMIZATION_KEY_${KEY_ID^^}"
API_PROJECT="legado-backend/src/Legado.Api"

read_key() { grep -E "^${NAME}=.+" "$1" 2>/dev/null | tail -n 1 | cut -d= -f2- || true; }

touch "$ENV_FILE"
key="$(read_key "$ENV_FILE")"
if [[ -n "$key" ]]; then
  echo "${NAME} já existe no .env."
else
  key="$(read_key "$CADASTRO_ENV")"
  if [[ -n "$key" ]]; then
    echo "${NAME} copiada de ${CADASTRO_ENV} (mesma chave nos dois sistemas)."
  else
    command -v openssl >/dev/null || { echo "openssl não encontrado." >&2; exit 1; }
    key="$(openssl rand -base64 32)"
    echo "${NAME} gerada. Use o mesmo valor no ${CADASTRO_ENV}."
  fi
  sed -i "/^${NAME}=/d" "$ENV_FILE"
  printf '%s=%s\n' "$NAME" "$key" >> "$ENV_FILE"
fi

if command -v dotnet >/dev/null; then
  # JSON pela entrada padrão (printf é builtin): a chave não aparece na lista de processos.
  printf '{"Logging:Pseudonymization:Keys:%s":"%s"}' "$KEY_ID" "$key" | dotnet user-secrets set --project "$API_PROJECT" >/dev/null
  echo "user-secrets do Legado.Api atualizado (Logging:Pseudonymization:Keys:${KEY_ID})."
fi
