#!/bin/sh
# =============================================================================
# H7: Database Backup Script — Docker-native, runs inside backup-scheduler
# =============================================================================
# Triggered by ofelia scheduler every 24h
# Env var DB_PASSWORD must be set in docker-compose
# =============================================================================

set -e

BACKUP_DIR="/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
FILE="${BACKUP_DIR}/LoanFlowLite_backup_${TIMESTAMP}.sql.gz"

echo "=== LoanFlow DB Backup: ${TIMESTAMP} ==="

# Ensure backup dir exists
mkdir -p "$BACKUP_DIR"

# Run mysqldump and compress output
mysqldump \
    -h db \
    -P 3306 \
    -u loanflow \
    -p"${DB_PASSWORD}" \
    --single-transaction \
    --routines \
    --triggers \
    --skip-lock-tables \
    LoanFlowLite | gzip > "$FILE"

echo "✅ Backup complete: $FILE ($(du -sh "$FILE" | cut -f1))"

# Prune backups older than 30 days (H7: disk management)
PRUNED=$(find "$BACKUP_DIR" -name "*.sql.gz" -mtime +30 -print)
if [ -n "$PRUNED" ]; then
    echo "🗑️  Pruning old backups:"
    echo "$PRUNED"
    find "$BACKUP_DIR" -name "*.sql.gz" -mtime +30 -delete
fi

echo "=== Backup job finished ==="
