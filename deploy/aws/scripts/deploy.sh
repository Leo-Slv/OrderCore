#!/usr/bin/env bash
# Deploys one commit of the demo. Run on the server (as root) by CI through
# SSM, and by the bootstrap for the first version:
#   /opt/ordercore/deploy.sh <full commit sha>
#
# 1. fetch deploy/aws at that commit   2. render the settings from SSM
# 3. start the data services           4. back up the database
# 5. pull the images                   6. migrate with the new image
# 7. start the new version             8. wait for /health/ready
#
# A failure before step 7 leaves the running version untouched. After it, the
# previous version is started again (its database schema may already have
# moved forward: migrations are forward-only, see Docs/operations/deployment.md).
set -euo pipefail

SHA="${1:?usage: deploy.sh <full commit sha>}"
ROOT="${ORDERCORE_ROOT:-/opt/ordercore}"
REPO="${ORDERCORE_REPO:-Leo-Slv/OrderCore}"
RELEASE="$ROOT/releases/$SHA"
KEEP_RELEASES=3

log() { printf '\n==> %s\n' "$*"; }

log "Fetching deploy/aws at ${SHA}"
mkdir -p "$RELEASE"
curl -fsSL "https://codeload.github.com/${REPO}/tar.gz/${SHA}" \
  | tar -xz -C "$RELEASE" --strip-components=3 --wildcards '*/deploy/aws/*'

log "Rendering the settings from SSM"
"$RELEASE/scripts/render-env.sh" "$SHA" "$RELEASE"

# The scripts and the units the server keeps using between deploys.
install -m 755 "$RELEASE/scripts/deploy.sh" "$ROOT/deploy.sh"
install -m 644 "$RELEASE"/systemd/ordercore-backup.* /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now ordercore-backup.timer >/dev/null

previous=""
if [[ -L "$ROOT/current" ]]; then
  previous="$(basename "$(readlink -f "$ROOT/current")")"
fi

cd "$RELEASE"
dc() { docker compose --env-file stack.env "$@"; }

log "Starting the data services"
dc up -d --wait postgres rabbitmq mailpit

log "Backing up the database"
"$RELEASE/scripts/backup.sh" "pre-deploy-${SHA:0:12}"

log "Pulling the images"
dc pull api caddy

log "Applying the migrations (the running version stays up if this fails)"
dc run --rm --no-deps api migrate

log "Starting the new version"
ln -sfn "$RELEASE" "$ROOT/current"
dc up -d --remove-orphans

api_host="api.$(grep '^PUBLIC_IP_DASHED=' stack.env | cut -d= -f2).sslip.io"
healthy=false
for _ in $(seq 1 60); do
  if curl -fsS -o /dev/null --max-time 5 "https://${api_host}/health/ready"; then
    healthy=true
    break
  fi
  sleep 5
done

if [[ "$healthy" != true ]]; then
  echo "The API did not answer https://${api_host}/health/ready in time." >&2
  dc logs --tail 80 api caddy >&2 || true
  if [[ -n "$previous" && "$previous" != "$SHA" && -d "$ROOT/releases/$previous" ]]; then
    echo "Starting the previous version again (${previous})." >&2
    ln -sfn "$ROOT/releases/$previous" "$ROOT/current"
    (cd "$ROOT/releases/$previous" && docker compose --env-file stack.env up -d --remove-orphans) || true
  fi
  exit 1
fi

log "Healthy: https://${api_host}"
docker image prune -f >/dev/null
# Keep the newest releases only (the current one is always among them).
# shellcheck disable=SC2012
ls -1dt "$ROOT"/releases/*/ | tail -n +$((KEEP_RELEASES + 1)) | xargs -r rm -rf
