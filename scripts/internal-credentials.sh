#!/usr/bin/env bash
# Gera as credenciais da API interna de migração (ADR-0005), só para o ambiente local:
#   secrets/internal/ca/ca.key            chave da CA local (nunca montada em container)
#   secrets/internal/ca.crt               certificado da CA (público: o Keycloak e o Cadastro confiam só nele)
#   secrets/internal/server/legado-api.*  certificado TLS da API (SAN legado-api, 1 ano) montado no container
#   .env                                  LEGADO_INTERNAL_KEYCLOAK_KEY e LEGADO_INTERNAL_CADASTRO_KEY (HMAC, 256 bits)
# Idempotente: mantém a CA e as chaves existentes e só reemite o certificado ausente ou a menos de 30 dias do fim.
# Tudo fica em arquivos ignorados pelo git e pelo Docker (Art. IV.4). Uso: ./scripts/internal-credentials.sh
set -euo pipefail

# Git Bash converteria "/CN=..." em caminho do Windows.
export MSYS_NO_PATHCONV=1

# Caminhos relativos: o openssl nativo do Windows não entende "/c/..." nem "<(...)".
cd "$(dirname "$0")/.."
DIR="secrets/internal"
CA_KEY="$DIR/ca/ca.key"
CA_CRT="$DIR/ca.crt"
SERVER_KEY="$DIR/server/legado-api.key"
SERVER_CRT="$DIR/server/legado-api.crt"
SERVER_EXT="$DIR/server/legado-api.ext"
SERVER_CSR="$DIR/server/legado-api.csr"
ENV_FILE=".env"
RENEW_BEFORE_SECONDS=$((30 * 24 * 3600))

command -v openssl >/dev/null || { echo "openssl não encontrado." >&2; exit 1; }
mkdir -p "$DIR/ca" "$DIR/server"

if [[ ! -s "$CA_KEY" || ! -s "$CA_CRT" ]]; then
  openssl ecparam -name prime256v1 -genkey -noout -out "$CA_KEY"
  openssl req -x509 -new -key "$CA_KEY" -sha256 -days 1825 -subj "/CN=Legado Internal CA" \
    -addext "basicConstraints=critical,CA:TRUE,pathlen:0" -addext "keyUsage=critical,keyCertSign,cRLSign" \
    -out "$CA_CRT"
  chmod 600 "$CA_KEY"
  rm -f "$SERVER_CRT"
  echo "CA local criada."
fi

if [[ ! -s "$SERVER_CRT" || ! -s "$SERVER_KEY" ]] \
  || ! openssl x509 -checkend "$RENEW_BEFORE_SECONDS" -noout -in "$SERVER_CRT" >/dev/null; then
  openssl ecparam -name prime256v1 -genkey -noout -out "$SERVER_KEY"
  openssl req -new -key "$SERVER_KEY" -subj "/CN=legado-api" -out "$SERVER_CSR"
  printf '%s\n' "subjectAltName=DNS:legado-api" "basicConstraints=critical,CA:FALSE" \
    "keyUsage=critical,digitalSignature" "extendedKeyUsage=serverAuth" > "$SERVER_EXT"
  openssl x509 -req -in "$SERVER_CSR" -CA "$CA_CRT" -CAkey "$CA_KEY" -CAcreateserial -CAserial "$DIR/ca/ca.srl" \
    -days 365 -sha256 -extfile "$SERVER_EXT" -out "$SERVER_CRT"
  rm -f "$SERVER_CSR" "$SERVER_EXT"
  # Leitura pelo usuário não-root do container (UID diferente do dono do arquivo); ambiente só local.
  chmod 644 "$SERVER_KEY" "$SERVER_CRT"
  echo "Certificado legado-api emitido (válido por 1 ano)."
else
  echo "Certificado legado-api ainda válido."
fi

touch "$ENV_FILE"
ensure_key() {
  local name="$1"
  if grep -Eq "^${name}=.+" "$ENV_FILE"; then
    echo "${name} já existe no .env."
  else
    sed -i "/^${name}=/d" "$ENV_FILE"
    printf '%s=%s\n' "$name" "$(openssl rand -base64 32)" >> "$ENV_FILE"
    echo "${name} gerada no .env."
  fi
}
ensure_key LEGADO_INTERNAL_KEYCLOAK_KEY
ensure_key LEGADO_INTERNAL_CADASTRO_KEY
