#!/usr/bin/env bash
# Genera el par de claves RSA para JWT signing (RS256).
# Ejecutar una sola vez por entorno. Las claves se guardan en infra/keys/ (gitignored).
#
# Requisito: openssl instalado (incluido en Git Bash para Windows).
#
# Uso:
#   bash infra/scripts/generate-jwt-keys.sh
#   bash infra/scripts/generate-jwt-keys.sh --force   # sobreescribir claves existentes

set -euo pipefail

KEYS_DIR="$(cd "$(dirname "$0")/../../infra/keys" && pwd)"
PRIVATE_KEY="$KEYS_DIR/jwt-private.pem"
PUBLIC_KEY="$KEYS_DIR/jwt-public.pem"
FORCE=false

for arg in "$@"; do
  [[ "$arg" == "--force" ]] && FORCE=true
done

mkdir -p "$KEYS_DIR"

if [[ -f "$PRIVATE_KEY" && "$FORCE" == false ]]; then
  echo "Keys already exist at $KEYS_DIR"
  echo "Use --force to regenerate (WARNING: invalidates all existing tokens)."
  exit 0
fi

echo "Generating 4096-bit RSA key pair..."

openssl genrsa -out "$PRIVATE_KEY" 4096 2>/dev/null
chmod 600 "$PRIVATE_KEY"

openssl rsa -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY" 2>/dev/null
chmod 644 "$PUBLIC_KEY"

echo "Done."
echo "  Private key : $PRIVATE_KEY"
echo "  Public key  : $PUBLIC_KEY"
echo ""
echo "WARNING: Changing these keys invalidates all active JWT tokens."
echo "         Users will need to log in again after deployment."
