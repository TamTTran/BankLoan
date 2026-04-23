#!/bin/bash
# =============================================================================
# C9: Generate Internal CA + mTLS Certificates for LoanFlow
# =============================================================================
# Usage: chmod +x certs/gen-certs.sh && ./certs/gen-certs.sh
# IMPORTANT: The certs/ directory is in .gitignore. Never commit private keys.
# =============================================================================

set -e
CERTS_DIR="$(cd "$(dirname "$0")" && pwd)"
echo "Generating certificates in: $CERTS_DIR"

# ── 1. Internal Certificate Authority ───────────────────────────────────────
echo "[1/4] Generating Internal CA..."
openssl genrsa -out "$CERTS_DIR/ca.key" 4096
openssl req -new -x509 -days 3650 -key "$CERTS_DIR/ca.key" \
    -out "$CERTS_DIR/ca.crt" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=LoanFlow-Internal-CA"

# ── 2. API Server Certificate (used by Kestrel on port 8443) ────────────────
echo "[2/4] Generating API server certificate..."
openssl genrsa -out "$CERTS_DIR/api.key" 2048
openssl req -new -key "$CERTS_DIR/api.key" \
    -out "$CERTS_DIR/api.csr" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=api"
openssl x509 -req -days 365 -in "$CERTS_DIR/api.csr" \
    -CA "$CERTS_DIR/ca.crt" -CAkey "$CERTS_DIR/ca.key" -CAcreateserial \
    -out "$CERTS_DIR/api.crt" \
    -extfile <(printf "subjectAltName=DNS:api,DNS:localhost")

# ── 3. Nginx Client Certificate (used by Nginx for mTLS to API) ─────────────
echo "[3/4] Generating Nginx client certificate..."
openssl genrsa -out "$CERTS_DIR/nginx-client.key" 2048
openssl req -new -key "$CERTS_DIR/nginx-client.key" \
    -out "$CERTS_DIR/nginx-client.csr" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=nginx-client"
openssl x509 -req -days 365 -in "$CERTS_DIR/nginx-client.csr" \
    -CA "$CERTS_DIR/ca.crt" -CAkey "$CERTS_DIR/ca.key" -CAcreateserial \
    -out "$CERTS_DIR/nginx-client.crt"

# ── 4. Clean up CSR files ────────────────────────────────────────────────────
echo "[4/4] Cleaning up CSR files..."
rm -f "$CERTS_DIR"/*.csr "$CERTS_DIR"/*.srl

echo ""
echo "✅ Certificates generated successfully:"
ls -la "$CERTS_DIR"/*.crt "$CERTS_DIR"/*.key
echo ""
echo "IMPORTANT: Never commit private key files (*.key) to git."
