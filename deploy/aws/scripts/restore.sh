#!/usr/bin/env bash
# Restores a backup over the running database. DESTRUCTIVE: replaces what is
# there. Usage: restore.sh <s3-key> --yes
#   aws s3 ls s3://<bucket>/postgres/   # to find a key, e.g. postgres/20261008T060000Z-daily.dump
set -euo pipefail

KEY="${1:?usage: restore.sh <s3-key> --yes}"
if [[ "${2:-}" != "--yes" ]]; then
  echo "This replaces the demo's database with ${KEY}. Run again with --yes to confirm." >&2
  exit 1
fi
BACKUP_ENV="${BACKUP_ENV:-/opt/ordercore/backup.env}"
# shellcheck disable=SC1090
source "$BACKUP_ENV"
: "${BACKUP_BUCKET:?BACKUP_BUCKET is not set in $BACKUP_ENV}"

compose() {
  (cd /opt/ordercore/current && docker compose --env-file stack.env "$@")
}
container="$(docker ps -q \
  --filter label=com.docker.compose.project=ordercore \
  --filter label=com.docker.compose.service=postgres)"
if [[ -z "$container" ]]; then
  echo "The postgres container is not running." >&2
  exit 1
fi

compose stop api
aws s3 cp "s3://${BACKUP_BUCKET}/${KEY}" - --only-show-errors \
  | docker exec -i "$container" pg_restore -U ordercore -d ordercore --clean --if-exists --no-owner
compose start api
echo "Restored ${KEY}."
