#!/bin/sh
set -e

# ─── Config ─────────────────────────────────────────────────────────────────
DB_SOURCE="/app/data/SalesApp.db"
WORK_DIR="/tmp/db_backup"
VACUUMED_DB="$WORK_DIR/SalesApp_vacuumed.db"
S3_DEST="mys3:${S3_BUCKET_NAME}/db"
INTERVAL_HOURS="${BACKUP_INTERVAL_HOURS:-24}"
RCLONE_CONFIG="/config/rclone.conf"

# ─── Helpers ─────────────────────────────────────────────────────────────────
log() {
    echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] $*"
}

die() {
    log "ERROR: $*" >&2
    exit 1
}

# ─── Preflight checks ────────────────────────────────────────────────────────
check_config() {
    [ -f "$RCLONE_CONFIG" ] || die "rclone config not found at $RCLONE_CONFIG. Create it from rclone.conf.example."
    [ -n "$S3_BUCKET_NAME" ] || die "S3_BUCKET_NAME env var is not set."
    command -v sqlite3 >/dev/null 2>&1 || die "sqlite3 not found in the image."
    command -v rclone >/dev/null 2>&1 || die "rclone not found in the image."
}

# ─── Vacuum ──────────────────────────────────────────────────────────────────
# Uses VACUUM INTO, which writes a compacted copy to a new file without
# locking or disrupting the live database (safe with WAL mode + active API).
run_vacuum() {
    mkdir -p "$WORK_DIR"
    rm -f "$VACUUMED_DB"

    log "Running VACUUM INTO '$VACUUMED_DB' ..."
    sqlite3 "$DB_SOURCE" "VACUUM INTO '$VACUUMED_DB';"
    log "VACUUM complete. Size: $(du -sh "$VACUUMED_DB" | cut -f1)"
}

# ─── Upload ──────────────────────────────────────────────────────────────────
run_sync() {
    TIMESTAMP="$(date -u '+%Y%m%d_%H%M%S')"
    REMOTE_FILE="$S3_DEST/SalesApp_${TIMESTAMP}.db"

    log "Uploading '$VACUUMED_DB' → '$REMOTE_FILE' ..."
    if ! rclone --config "$RCLONE_CONFIG" copyto "$VACUUMED_DB" "$REMOTE_FILE"; then
        log "ERROR: Upload to S3 failed. Check rclone.conf credentials and IAM policy."
        return 1
    fi
    log "Upload complete."

    # Keep only the latest N backups in S3 (default: 7)
    MAX_BACKUPS="${MAX_BACKUPS:-7}"
    log "Pruning old backups (keeping latest $MAX_BACKUPS) ..."
    rclone --config "$RCLONE_CONFIG" ls "$S3_DEST" \
        | sort | head -n "-$MAX_BACKUPS" \
        | awk '{print $2}' \
        | while IFS= read -r old_file; do
            log "  Deleting old backup: $old_file"
            rclone --config "$RCLONE_CONFIG" delete "$S3_DEST/$old_file"
        done
    log "Pruning done."
}

# ─── Cleanup ─────────────────────────────────────────────────────────────────
cleanup() {
    rm -f "$VACUUMED_DB"
}

# ─── Main loop ───────────────────────────────────────────────────────────────
check_config

log "Backup service started. Interval: ${INTERVAL_HOURS}h | Bucket: $S3_BUCKET_NAME | Max backups: ${MAX_BACKUPS:-7}"

while true; do
    log "─── Starting backup cycle ───────────────────────────────────────"

    if [ ! -f "$DB_SOURCE" ]; then
        log "WARN: Database '$DB_SOURCE' not found yet. Waiting..."
    else
        run_vacuum && run_sync || log "ERROR: Backup cycle failed."
        cleanup
    fi

    log "─── Next backup in ${INTERVAL_HOURS}h ──────────────────────────────────────"
    sleep "$(( INTERVAL_HOURS * 3600 ))"
done
