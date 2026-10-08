#!/usr/bin/env bash
# Loads OrderCore's dashboards and alert rules into a Grafana (Cloud's free
# tier for the AWS demo; Docs/operations/aws-demo.md), and sends alerts by
# e-mail. Run it locally; it only talks to Grafana's HTTP API.
#
#   GRAFANA_URL=https://<stack>.grafana.net \
#   GRAFANA_TOKEN=<service account token, role Admin> \
#   ALERT_EMAIL=you@example.com \
#   ./import.sh
#
# Needs curl, jq and yq (mikefarah). The dashboards and rules in the
# repository point at a datasource whose uid is "prometheus" (the local
# grafana/otel-lgtm); Grafana Cloud's Prometheus has another uid, found here
# and swapped in (override with PROMETHEUS_UID). DRY_RUN=1 prints what would
# be sent instead of sending it.
# jq programs use $variables inside single quotes on purpose.
# shellcheck disable=SC2016
set -euo pipefail

: "${GRAFANA_URL:?Set GRAFANA_URL, e.g. https://mystack.grafana.net}"
: "${GRAFANA_TOKEN:?Set GRAFANA_TOKEN (a service account token with the Admin role)}"
: "${ALERT_EMAIL:?Set ALERT_EMAIL (where alerts are sent)}"
GRAFANA_URL="${GRAFANA_URL%/}"
JQ="${JQ:-jq}"
YQ="${YQ:-yq}"
DRY_RUN="${DRY_RUN:-}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FOLDER_UID="ordercore"
CONTACT_POINT="ordercore-email"

api() { # api <method> <path> [json body]; prints the body, fails on HTTP >= 400
  local method="$1" path="$2" body="${3:-}"
  if [[ -n "$DRY_RUN" ]]; then
    echo "DRY RUN: $method $path" >&2
    return 0
  fi
  local args=(-sS -X "$method" -H "Authorization: Bearer $GRAFANA_TOKEN" -H "Content-Type: application/json" \
    -H "X-Disable-Provenance: true" -w '\n%{http_code}')
  [[ -n "$body" ]] && args+=(--data "$body")
  local out status
  out="$(curl "${args[@]}" "$GRAFANA_URL$path")"
  status="${out##*$'\n'}"
  out="${out%$'\n'*}"
  if ((status >= 400)); then
    printf '%s' "$out" >&2
    return "$status"
  fi
  printf '%s' "$out"
}

echo "Looking for the Prometheus datasource"
if [[ -n "${PROMETHEUS_UID:-}" ]]; then
  prom_uid="$PROMETHEUS_UID"
elif [[ -n "$DRY_RUN" ]]; then
  prom_uid="dry-run-uid"
else
  prom_uid="$(api GET /api/datasources | "$JQ" -r '[.[] | select(.type == "prometheus")][0].uid // empty')"
fi
[[ -n "$prom_uid" ]] || { echo "No Prometheus datasource found; set PROMETHEUS_UID." >&2; exit 1; }
echo "  using datasource uid $prom_uid"

echo "Folder"
api POST /api/folders "$("$JQ" -n --arg uid "$FOLDER_UID" '{uid: $uid, title: "OrderCore"}')" >/dev/null || true

echo "Dashboards"
for file in "$HERE"/dashboards/*.json; do
  body="$("$JQ" --arg prom "$prom_uid" --arg folder "$FOLDER_UID" \
    '{dashboard: (walk(if type == "object" and .uid == "prometheus" and has("type") then .uid = $prom else . end) | .id = null),
      folderUid: $folder, overwrite: true}' "$file")"
  api POST /api/dashboards/db "$body" >/dev/null
  echo "  $(basename "$file")"
done

echo "Alert rules"
"$YQ" -o=json '.groups[0].rules[]' "$HERE/alerting/ordercore-rules.yaml" | "$JQ" -c '.' | while read -r rule; do
  uid="$(printf '%s' "$rule" | "$JQ" -r '.uid')"
  body="$(printf '%s' "$rule" | "$JQ" --arg prom "$prom_uid" --arg folder "$FOLDER_UID" \
    '.folderUID = $folder | .ruleGroup = "OrderCore" | .orgID = 1
     | .data |= map(if .datasourceUid == "prometheus" then .datasourceUid = $prom else . end)')"
  if ! api PUT "/api/v1/provisioning/alert-rules/$uid" "$body" >/dev/null 2>&1; then
    api POST /api/v1/provisioning/alert-rules "$body" >/dev/null
  fi
  echo "  $uid"
done

echo "E-mail contact point and default policy"
contact="$("$JQ" -n --arg name "$CONTACT_POINT" --arg to "$ALERT_EMAIL" \
  '{name: $name, type: "email", settings: {addresses: $to, singleEmail: true}}')"
if ! api POST /api/v1/provisioning/contact-points "$contact" >/dev/null 2>&1; then
  uid="$(api GET /api/v1/provisioning/contact-points | "$JQ" -r --arg name "$CONTACT_POINT" '[.[] | select(.name == $name)][0].uid // empty')"
  [[ -n "$uid" ]] && api PUT "/api/v1/provisioning/contact-points/$uid" "$contact" >/dev/null
fi
api PUT /api/v1/provisioning/policies "$("$JQ" -n --arg receiver "$CONTACT_POINT" '{receiver: $receiver}')" >/dev/null
echo "Done: $GRAFANA_URL/alerting/list"
