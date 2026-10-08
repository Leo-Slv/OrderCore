#!/usr/bin/env bash
# Creates the demo's secrets and settings in SSM Parameter Store under
# /ordercore/demo/ (Docs/operations/aws-demo.md lists every one). Run it
# locally, with your AWS credentials, BEFORE creating the CloudFormation stack
# (and again whenever you want to add or change something).
#
#   AWS_REGION=us-east-1 ./create-parameters.sh
#
# - Passwords and keys that can be generated are generated, once: an existing
#   parameter is left alone (use ROTATE=name1,name2 to replace specific ones).
# - Anything optional is asked for; press Enter to skip it. An environment
#   variable with the same purpose (STRIPE_SECRET_KEY, ...) skips the question.
# - Only parameter names are printed, plus the admin password and the e-mail
#   inbox password ONCE, when they are generated: keep them.
set -euo pipefail

PREFIX="/ordercore/demo"
REGION="${AWS_REGION:-${AWS_DEFAULT_REGION:-us-east-1}}"
ROTATE=",${ROTATE:-},"
export AWS_DEFAULT_REGION="$REGION"

exists() { aws ssm get-parameter --name "$PREFIX/$1" >/dev/null 2>&1; }
should_write() { ! exists "$1" || [[ "$ROTATE" == *",$1,"* ]]; }
put() { # put <name> <type> <value>
  aws ssm put-parameter --name "$PREFIX/$1" --type "$2" --value "$3" --overwrite >/dev/null
  echo "  wrote $PREFIX/$1"
}
random_hex() { openssl rand -hex "$1"; }
random_password() { # letters and digits, at least one of each
  local value
  while :; do
    value="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 20)"
    if [[ "$value" =~ [A-Za-z] && "$value" =~ [0-9] && ${#value} -ge 16 ]]; then
      printf '%s' "$value"
      return
    fi
  done
}
ask() { # ask <variable> <prompt> [secret]
  local -n target="$1"
  local prompt="$2" secret="${3:-}"
  if [[ -n "${target:-}" ]]; then
    return
  fi
  if [[ "$secret" == secret ]]; then
    read -r -s -p "$prompt (Enter to skip): " target || true
    echo
  else
    read -r -p "$prompt (Enter to skip): " target || true
  fi
}

echo "Region: $REGION"
echo "Generated parameters:"

if should_write jwt-signing-key; then put jwt-signing-key SecureString "$(openssl rand -base64 48)"; fi
if should_write postgres-password; then put postgres-password SecureString "$(random_hex 24)"; fi
if should_write rabbitmq-password; then put rabbitmq-password SecureString "$(random_hex 24)"; fi

if should_write admin-email; then
  ADMIN_EMAIL="${ADMIN_EMAIL:-admin@marfim.demo}"
  put admin-email String "$ADMIN_EMAIL"
fi
if should_write admin-password; then
  admin_password="$(random_password)"
  put admin-password SecureString "$admin_password"
  echo "  >> admin password (shown once): $admin_password"
fi

if should_write mailpit-user; then put mailpit-user String "${MAILPIT_USER:-demo}"; fi
if should_write mailpit-password-hash; then
  inbox_password="$(random_password)"
  hash="$(docker run --rm caddy:2-alpine caddy hash-password --plaintext "$inbox_password")"
  put mailpit-password-hash SecureString "$hash"
  echo "  >> e-mail inbox password (shown once): $inbox_password"
fi

echo
echo "Optional settings:"
ask STOREFRONT_URL "Storefront URL, e.g. https://marfim.vercel.app (CORS and e-mail links)"
ask STRIPE_SECRET_KEY "Stripe TEST secret key (sk_test_...)" secret
ask STRIPE_PUBLISHABLE_KEY "Stripe TEST publishable key (pk_test_...)"
ask STRIPE_WEBHOOK_SECRET "Stripe webhook signing secret (whsec_..., after registering the endpoint)" secret
ask OTLP_ENDPOINT "Grafana Cloud OTLP endpoint (https://otlp-gateway-....grafana.net/otlp)"
ask OTLP_HEADERS "Grafana Cloud OTLP headers (Authorization=Basic ...)" secret
ask RESEND_API_KEY "Resend API key (re_...) to deliver real e-mail instead of the inbox" secret
if [[ -n "${RESEND_API_KEY:-}" ]]; then
  ask MAIL_FROM "Sender on a domain verified in Resend, e.g. Marfim <no-reply@your.domain>"
fi

if [[ -n "${STOREFRONT_URL:-}" ]]; then put storefront-url String "$STOREFRONT_URL"; fi
if [[ -n "${STRIPE_SECRET_KEY:-}" ]]; then
  if [[ "$STRIPE_SECRET_KEY" != sk_test_* ]]; then
    echo "Refusing: the demo only takes Stripe TEST keys (sk_test_...)." >&2
    exit 1
  fi
  put stripe-secret-key SecureString "$STRIPE_SECRET_KEY"
fi
if [[ -n "${STRIPE_PUBLISHABLE_KEY:-}" ]]; then put stripe-publishable-key String "$STRIPE_PUBLISHABLE_KEY"; fi
if [[ -n "${STRIPE_WEBHOOK_SECRET:-}" ]]; then put stripe-webhook-secret SecureString "$STRIPE_WEBHOOK_SECRET"; fi
if [[ -n "${OTLP_ENDPOINT:-}" ]]; then put otlp-endpoint String "$OTLP_ENDPOINT"; fi
if [[ -n "${OTLP_HEADERS:-}" ]]; then put otlp-headers SecureString "$OTLP_HEADERS"; fi
if [[ -n "${RESEND_API_KEY:-}" ]]; then put resend-api-key SecureString "$RESEND_API_KEY"; fi
if [[ -n "${MAIL_FROM:-}" ]]; then put mail-from String "$MAIL_FROM"; fi

echo
echo "Done. Parameters under $PREFIX:"
aws ssm get-parameters-by-path --path "$PREFIX" --query 'Parameters[].Name' --output text | tr '\t' '\n' | sed 's/^/  /'
