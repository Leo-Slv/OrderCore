# The OrderCore demo on a free VM (S3 only on AWS)

Runbook for running the demo stack of `deploy/aws` on a server **outside AWS**
— the case where paying for an EC2 instance is not worth it. AWS is used for
one thing: an S3 bucket that holds the daily PostgreSQL backups. Everything
else (API, PostgreSQL, RabbitMQ, the e-mail inbox, HTTPS) runs on the VM,
from the same `compose.yml`, Caddyfile and scripts as the EC2 variant
(`Docs/operations/aws-demo.md`; read its "What the demo looks like" and
"Operating it" sections, they apply here too).

```text
GitHub Actions (master green)
  ├─ build, test ─► publish ghcr.io/leo-slv/ordercore-api (amd64 + arm64)
  └─ deploy-demo-ssh ── SSH ─► VM: sudo /opt/ordercore/deploy.sh <sha>

VM (Oracle Cloud Always Free, Ubuntu, ARM)      AWS (only this)
  caddy → api · postgres · rabbitmq · mailpit    S3 bucket, 30-day lifecycle
  /opt/ordercore/params.env (root, 600)  ──────► IAM user: that bucket only
  systemd timer ─► backup.sh ──────────────────► s3://<bucket>/postgres/
```

## What you need

- An AWS account and the AWS CLI locally (for the bucket and the backup user).
- A free VM. The steps below use **Oracle Cloud Always Free**, an Ampere A1
  instance (e.g. 2 OCPU and 12 GB), Ubuntu 24.04 (aarch64), with your SSH key.
  Any Ubuntu server with a public IPv4 works the same way.
- A Stripe **test-mode** account (test keys only).
- The Resend key you have is for development only: **leave it out**. Without a
  verified domain Resend only delivers to the account owner's address, so a
  visitor would never receive the confirmation e-mail (and checkout requires a
  confirmed e-mail). The default — the demo's Mailpit inbox behind a password —
  works for everyone who has that password. See "E-mail" below.
- The OrderCore repository public on GitHub (the server fetches `deploy/aws`
  from it) and its image public in GHCR (`aws-demo.md`, step 1).

## 1. The bucket and the backup user (AWS)

```bash
aws cloudformation deploy \
  --stack-name ordercore-demo-backup \
  --template-file deploy/aws/backup-bucket.yaml \
  --capabilities CAPABILITY_NAMED_IAM
aws cloudformation describe-stacks --stack-name ordercore-demo-backup --query 'Stacks[0].Outputs'
aws iam create-access-key --user-name ordercore-demo-backup
```

Note the bucket name and the access key pair it prints (shown once). The user
can only read and write that bucket. Storage of a small database costs cents
per month; nothing is billed by the hour.

## 2. The VM

1. Create the instance (Compute → Instances; shape `VM.Standard.A1.Flex`,
   image Ubuntu 24.04 aarch64) with your SSH public key. If Oracle answers "out
   of host capacity", try another availability domain or retry later: A1
   capacity is scarce.
2. Reserve the public IP so it survives restarts (Networking → Reserved public
   IPs → attach it to the instance's VNIC); the certificates' names contain it.
3. In the VCN's security list (or network security group), allow ingress TCP
   **80** and **443** from `0.0.0.0/0` (22 is already open).
4. SSH in and run the setup (Docker, the AWS CLI, the host firewall, the
   scripts):

   ```bash
   curl -fsSL https://raw.githubusercontent.com/Leo-Slv/OrderCore/master/deploy/aws/scripts/bootstrap-ubuntu.sh | sudo bash
   ```

   It needs the script on `master`, so do step 1 of `aws-demo.md` (merge and a
   green CI) first.

## 3. Credentials and parameters (on the VM)

```bash
sudo aws configure                      # the backup user's access key; region us-east-1
sudo PARAMS_FILE=/opt/ordercore/params.env /opt/ordercore/create-parameters.sh
```

The second command generates the secrets (JWT key, database and broker
passwords, the admin password, the inbox password — **the last two are shown
once, keep them**) into `/opt/ordercore/params.env` (root only) and asks for:
the bucket, the Stripe test keys (skip the webhook secret for now), and the
storefront URL if you know it. Re-run it any time to add or change something;
existing secrets are kept (`ROTATE=name1,name2` replaces specific ones).

## 4. First deploy

Pick a commit of `master` whose CI run published the image, then:

```bash
sudo /opt/ordercore/deploy.sh <full commit sha>
```

It fetches `deploy/aws` at that commit, renders the settings, starts the data
services, backs up, migrates, starts the API and waits for
`https://api.<ip-dashed>.sslip.io/health/ready` (the first run also waits for
Caddy to obtain the certificate). The API's address is printed at the end.

## 5. Stripe webhook and the storefront

- Stripe dashboard (test mode) → Webhooks → add `<ApiUrl>/api/payments/webhooks/stripe`
  with the events listed in `aws-demo.md` step 4; then store its secret:
  `sudo PARAMS_FILE=/opt/ordercore/params.env /opt/ordercore/create-parameters.sh`
  (enter the whsec_ value) and deploy again.
- Marfim on Vercel: `NEXT_PUBLIC_API_URL=<ApiUrl>`; then store the Vercel URL as
  `storefront-url` (same command) and deploy again, so CORS and the e-mail
  links point at it.

## 6. Let GitHub deploy (optional)

Without it, deploy by hand with step 4. To deploy on every green `master`:
create a deploy key (`ssh-keygen -t ed25519 -f deploy_key -N ""`), append
`deploy_key.pub` to the VM user's `~/.ssh/authorized_keys`, and in the
repository's **demo** environment set the variables `DEMO_SSH_HOST` (the IP),
`DEMO_SSH_USER` (default `ubuntu`) and `DEMO_API_URL`, and the secret
`DEMO_SSH_KEY` (the private key; optionally `DEMO_SSH_KNOWN_HOSTS` from
`ssh-keyscan <ip>`, otherwise the host key is trusted on first use). The user
needs passwordless `sudo` (the default on the Ubuntu images).

## E-mail

- **Default (recommended for this demo)**: Mailpit. Confirmation, password
  reset and order e-mails land in `https://mail.<ip-dashed>.sslip.io` (user
  `demo`, the inbox password from step 3). A visitor can't open it, so for
  strangers testing the store, share the inbox password (the confirmation link
  is in the e-mail there).
- **Your dev Resend key** only works for sending *to the e-mail of your Resend
  account*, from `onboarding@resend.dev`. If that is enough (you are the only
  one testing), store `resend-api-key` and `mail-from` =
  `Marfim <onboarding@resend.dev>` in `params.env` and deploy again. Real
  visitors need a verified domain.

## Differences from the EC2 variant

| | EC2 (`aws-demo.md`) | Free VM (this runbook) |
|---|---|---|
| Parameters | SSM Parameter Store | `/opt/ordercore/params.env` on the VM |
| Deploy from CI | SSM `SendCommand` (no open port) | SSH (`deploy-demo-ssh`) |
| Backups | S3 through the instance role | S3 through the backup user's key |
| Cost | ~US$ 18 / month | cents (S3 only) |

## Risks

- **Oracle may reclaim an idle Always Free instance** (sustained very low CPU,
  network and memory use for a week). A demo with no traffic can hit that; the
  backups in S3 are what makes it recoverable.
- **Everything is on one disk**: the daily S3 dump and the dump before each
  deploy are the only copy that survives losing the VM.
- **The SSH key in GitHub** can run `deploy.sh` as root on the VM: use a key
  made only for this, in the `demo` environment.
- The `sslip.io` and 2 GB notes of `aws-demo.md` apply too (here the VM has more
  memory).
