#!/usr/bin/env bash
# Renders the demo's configuration from SSM Parameter Store (/ordercore/demo/*)
# into the files compose reads. Never echoes a value.
#
# Usage: render-env.sh <image-tag> <output-dir>
#   api.env             the API's settings (env_file)
#   stack.env           what compose interpolates (--env-file)
#   mailpit-auth.caddy  "<user> <bcrypt hash>" for Caddy's basic_auth
#   /opt/ordercore/backup.env  the backup bucket, for the backup timer
#
# Parameters (docs: Docs/operations/aws-demo.md). Required: jwt-signing-key,
# postgres-password, rabbitmq-password, admin-email, admin-password,
# mailpit-user, mailpit-password-hash, backup-bucket. Optional: storefront-url,
# stripe-secret-key + stripe-publishable-key + stripe-webhook-secret,
# resend-api-key + mail-from, otlp-endpoint + otlp-headers.
set -euo pipefail
umask 077

TAG="${1:?usage: render-env.sh <image-tag> <output-dir>}"
OUT="${2:?usage: render-env.sh <image-tag> <output-dir>}"
PREFIX="${SSM_PREFIX:-/ordercore/demo}"
BACKUP_ENV="${BACKUP_ENV:-/opt/ordercore/backup.env}"

declare -A P=()
while IFS=$'\t' read -r name value; do
  P["${name#"$PREFIX"/}"]="$value"
done < <(aws ssm get-parameters-by-path --path "$PREFIX" --with-decryption --recursive \
  --query 'Parameters[].[Name,Value]' --output text)

missing=()
# need <variable> <parameter>: copies a required parameter into a variable (no
# subshell, so the list of missing ones survives).
need() {
  local -n target="$1"
  target="${P[$2]:-}"
  if [[ -z "$target" ]]; then
    missing+=("$PREFIX/$2")
  fi
}
opt() { printf '%s' "${P[$1]:-}"; }

# The address: PUBLIC_IP, else the stack's public-ip parameter, else the
# instance metadata (IMDSv2).
if [[ -z "${PUBLIC_IP:-}" && -n "${P[public-ip]:-}" ]]; then
  PUBLIC_IP="${P[public-ip]}"
fi
if [[ -n "${PUBLIC_IP:-}" ]]; then
  ip="$PUBLIC_IP"
else
  token="$(curl -fsS -X PUT http://169.254.169.254/latest/api/token -H 'X-aws-ec2-metadata-token-ttl-seconds: 60')"
  ip="$(curl -fsS -H "X-aws-ec2-metadata-token: $token" http://169.254.169.254/latest/meta-data/public-ipv4)"
fi
dashed="${ip//./-}"
api_host="api.${dashed}.sslip.io"

jwt='' pg='' mq='' admin_email='' admin_password='' mail_user='' mail_hash='' bucket=''
need jwt jwt-signing-key
need pg postgres-password
need mq rabbitmq-password
need admin_email admin-email
need admin_password admin-password
need mail_user mailpit-user
need mail_hash mailpit-password-hash
need bucket backup-bucket
if ((${#missing[@]})); then
  printf 'Missing SSM parameters:\n' >&2
  printf '  %s\n' "${missing[@]}" >&2
  exit 1
fi

# The storefront: an origin for CORS and the pages the account e-mails link
# to. Until its real address is stored, the reserved name keeps the API valid.
storefront="$(opt storefront-url)"
storefront="${storefront:-https://loja.${dashed}.sslip.io}"
storefront="${storefront%/}"
origin="$(printf '%s' "$storefront" | sed -E 's#^(https?://[^/]+).*#\1#')"

mkdir -p "$OUT"
{
  echo "ASPNETCORE_ENVIRONMENT=Production"
  echo "AllowedHosts=${api_host}"
  # Only Caddy can reach the API, so the proxy's headers are trusted.
  echo "ForwardedHeaders__TrustAllProxies=true"
  echo "HTTPS_PORT=443"
  echo "ConnectionStrings__OrderCoreDb=Host=postgres;Port=5432;Database=ordercore;Username=ordercore;Password=${pg}"
  echo "Jwt__SigningKey=${jwt}"
  echo "RabbitMq__Host=rabbitmq"
  echo "RabbitMq__Username=ordercore"
  echo "RabbitMq__Password=${mq}"
  echo "IdentitySeed__AdminEmail=${admin_email}"
  echo "IdentitySeed__AdminPassword=${admin_password}"
  echo "Cors__AllowedOrigins__0=${origin}"
  echo "Identity__Links__ResetPassword=${storefront}/redefinir-senha?token={token}"
  echo "Identity__Links__ConfirmEmail=${storefront}/confirmar-email?token={token}"
  echo "OpenApi__Expose=true"
  echo "Observability__TraceSamplingRatio=0.2"

  # The Marfim storefront's look in e-mail (Docs/specs/notifications/email-branding.md).
  echo "Notifications__Brand__Name=Marfim"
  echo "Notifications__Brand__Wordmark=marfim"
  echo "Notifications__Brand__WordmarkAccent=."
  echo "Notifications__Brand__AccentColor=#3b3fd9"
  echo "Notifications__Brand__AccentSoftColor=#ececfd"
  echo "Notifications__Brand__Footer=Você recebeu este e-mail porque tem uma conta na Marfim. Mensagem automática sobre a sua conta ou pedido. Loja de demonstração."

  # E-mail: Mailpit by default; a Resend key switches to real delivery.
  if [[ -n "$(opt resend-api-key)" ]]; then
    echo "Notifications__Resend__ApiKey=$(opt resend-api-key)"
    echo "Notifications__From=$(opt mail-from)"
  else
    echo "Notifications__Smtp__Host=mailpit"
    echo "Notifications__Smtp__Port=1025"
    echo "Notifications__From=Marfim <no-reply@${api_host}>"
  fi

  # Stripe test mode; all three or none (none = the fake provider).
  if [[ -n "$(opt stripe-secret-key)" ]]; then
    echo "Payments__Stripe__SecretKey=$(opt stripe-secret-key)"
    echo "Payments__Stripe__PublishableKey=$(opt stripe-publishable-key)"
    echo "Payments__Stripe__WebhookSecret=$(opt stripe-webhook-secret)"
  fi

  if [[ -n "$(opt otlp-endpoint)" ]]; then
    echo "OTEL_EXPORTER_OTLP_ENDPOINT=$(opt otlp-endpoint)"
    echo "OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf"
    echo "OTEL_EXPORTER_OTLP_HEADERS=$(opt otlp-headers)"
  fi
} >"$OUT/api.env"

{
  echo "IMAGE_TAG=sha-${TAG}"
  echo "PUBLIC_IP_DASHED=${dashed}"
  echo "POSTGRES_PASSWORD=${pg}"
  echo "RABBITMQ_PASSWORD=${mq}"
} >"$OUT/stack.env"

printf '%s %s\n' "$mail_user" "$mail_hash" >"$OUT/mailpit-auth.caddy"

mkdir -p "$(dirname "$BACKUP_ENV")"
printf 'BACKUP_BUCKET=%s\n' "$bucket" >"$BACKUP_ENV"

chmod 600 "$OUT/api.env" "$OUT/stack.env" "$OUT/mailpit-auth.caddy" "$BACKUP_ENV"
echo "Rendered ${OUT} for sha-${TAG} (${api_host}, storefront ${origin})."
