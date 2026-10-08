#!/usr/bin/env bash
# One-time setup of a server OUTSIDE AWS (Ubuntu 22.04/24.04, e.g. an Oracle
# Cloud Always Free Ampere VM): Docker with the compose plugin, the AWS CLI
# (only for the S3 backups), the host firewall opened for 80/443, and the deploy
# script. Run it as root. Docs/operations/free-vm-demo.md has the whole flow.
#
#   curl -fsSL https://raw.githubusercontent.com/Leo-Slv/OrderCore/master/deploy/aws/scripts/bootstrap-ubuntu.sh | sudo bash
set -euo pipefail

REPO="${ORDERCORE_REPO:-Leo-Slv/OrderCore}"
ROOT=/opt/ordercore

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Run as root (sudo)." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y ca-certificates curl unzip tar gzip iptables-persistent

if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh
fi
systemctl enable --now docker
docker compose version >/dev/null

if ! command -v aws >/dev/null 2>&1; then
  case "$(uname -m)" in
    aarch64 | arm64) bundle=awscli-exe-linux-aarch64.zip ;;
    *) bundle=awscli-exe-linux-x86_64.zip ;;
  esac
  tmp="$(mktemp -d)"
  curl -fsSL "https://awscli.amazonaws.com/$bundle" -o "$tmp/aws.zip"
  unzip -q "$tmp/aws.zip" -d "$tmp"
  "$tmp/aws/install"
  rm -rf "$tmp"
fi

# Oracle's Ubuntu images ship iptables rules that reject everything but SSH:
# let HTTP and HTTPS in, ahead of them (idempotent), and keep them across reboots.
for port in 80 443; do
  iptables -C INPUT -p tcp --dport "$port" -j ACCEPT 2>/dev/null \
    || iptables -I INPUT 1 -p tcp --dport "$port" -j ACCEPT
done
netfilter-persistent save

mkdir -p "$ROOT"
for script in deploy.sh create-parameters.sh; do
  curl -fsSL "https://raw.githubusercontent.com/${REPO}/master/deploy/aws/scripts/${script}" -o "$ROOT/${script}"
  chmod +x "$ROOT/${script}"
done

cat <<EOF

Server ready. Next (Docs/operations/free-vm-demo.md):
  1. open ports 80 and 443 in the cloud provider's network rules (not only here);
  2. aws configure                       (the backup user's access key)
  3. sudo PARAMS_FILE=$ROOT/params.env AWS_DEFAULT_REGION=us-east-1 $ROOT/create-parameters.sh
  4. $ROOT/deploy.sh <full commit sha of a published master>
EOF
