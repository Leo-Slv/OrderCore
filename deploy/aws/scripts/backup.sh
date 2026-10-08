#!/usr/bin/env bash
# Dumps the PostgreSQL database straight to S3 (custom format, restorable
# with restore.sh). Usage: backup.sh [label]   (default label: daily)
# Run daily by the ordercore-backup timer and by deploy.sh before a deploy.
set -euo pipefail

LABEL="${1:-daily}"
BACKUP_ENV="${BACKUP_ENV:-/opt/ordercore/backup.env}"
# shellcheck disable=SC1090
source "$BACKUP_ENV"
: "${BACKUP_BUCKET:?BACKUP_BUCKET is not set in $BACKUP_ENV}"

container="$(docker ps -q \
  --filter label=com.docker.compose.project=ordercore \
  --filter label=com.docker.compose.service=postgres)"
if [[ -z "$container" ]]; then
  echo "The postgres container is not running." >&2
  exit 1
fi

key="postgres/$(date -u +%Y%m%dT%H%M%SZ)-${LABEL}.dump"
docker exec "$container" pg_dump -U ordercore -Fc ordercore \
  | aws s3 cp - "s3://${BACKUP_BUCKET}/${key}" --only-show-errors
echo "Backup written to s3://${BACKUP_BUCKET}/${key}"
