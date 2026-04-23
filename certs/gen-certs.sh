#!/bin/bash
# Generate local/internal certificates for LoanFlow.
# The certs directory is ignored by git. Never commit private keys.

set -e

CERTS_DIR="$(cd "$(dirname "$0")" && pwd)"
echo "Generating certificates in: $CERTS_DIR"

echo "[1/5] Generating internal CA..."
openssl genrsa -out "$CERTS_DIR/ca.key" 4096
openssl req -new -x509 -days 3650 -key "$CERTS_DIR/ca.key" \
    -out "$CERTS_DIR/ca.crt" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=LoanFlow-Internal-CA"

echo "[2/5] Generating API server certificate..."
openssl genrsa -out "$CERTS_DIR/api.key" 2048
openssl req -new -key "$CERTS_DIR/api.key" \
    -out "$CERTS_DIR/api.csr" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=api"
openssl x509 -req -days 365 -in "$CERTS_DIR/api.csr" \
    -CA "$CERTS_DIR/ca.crt" -CAkey "$CERTS_DIR/ca.key" -CAcreateserial \
    -out "$CERTS_DIR/api.crt" \
    -extfile <(printf "subjectAltName=DNS:api,DNS:localhost")

echo "[3/5] Generating Nginx public server certificate..."
openssl genrsa -out "$CERTS_DIR/nginx-server.key" 2048
openssl req -new -key "$CERTS_DIR/nginx-server.key" \
    -out "$CERTS_DIR/nginx-server.csr" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=loanflow.local"
openssl x509 -req -days 365 -in "$CERTS_DIR/nginx-server.csr" \
    -CA "$CERTS_DIR/ca.crt" -CAkey "$CERTS_DIR/ca.key" -CAcreateserial \
    -out "$CERTS_DIR/nginx-server.crt" \
    -extfile <(printf "subjectAltName=DNS:loanflow.local,DNS:localhost,IP:127.0.0.1")

echo "[4/5] Generating Nginx client certificate..."
openssl genrsa -out "$CERTS_DIR/nginx-client.key" 2048
openssl req -new -key "$CERTS_DIR/nginx-client.key" \
    -out "$CERTS_DIR/nginx-client.csr" \
    -subj "/C=VN/ST=HCM/O=LoanFlow/CN=nginx-client"
openssl x509 -req -days 365 -in "$CERTS_DIR/nginx-client.csr" \
    -CA "$CERTS_DIR/ca.crt" -CAkey "$CERTS_DIR/ca.key" -CAcreateserial \
    -out "$CERTS_DIR/nginx-client.crt"

echo "[5/5] Cleaning up CSR files..."
rm -f "$CERTS_DIR"/*.csr "$CERTS_DIR"/*.srl

echo ""
echo "Certificates generated successfully:"
ls -la "$CERTS_DIR"/*.crt "$CERTS_DIR"/*.key
echo ""
echo "IMPORTANT: Never commit private key files (*.key) to git."
