#!/usr/bin/env bash
# Cria a rede Docker compartilhada "workspace-data" se ainda não existir (idempotente).
# Nela o SQL Server do Legado fica alcançável pelo Cadastro (esquema import) sem publicar porta fora de 127.0.0.1.
# Uso: ./scripts/data-network.sh
set -euo pipefail

NETWORK_NAME="workspace-data"

if docker network inspect "$NETWORK_NAME" >/dev/null 2>&1; then
  echo "Rede ${NETWORK_NAME} já existe."
else
  docker network create "$NETWORK_NAME" >/dev/null
  echo "Rede ${NETWORK_NAME} criada."
fi
