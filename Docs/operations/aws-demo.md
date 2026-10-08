# The OrderCore demo on AWS

> Don't want to pay for the server? `Docs/operations/free-vm-demo.md` runs the same
> stack on a free VM outside AWS and uses AWS only for an S3 bucket of backups.

Runbook for `Docs/specs/operations/aws-demo-deployment.md`: the API, its
database, broker, e-mail inbox and HTTPS proxy on one ARM server, deployed by
CI. Everything is described by files in `deploy/aws`; nothing here creates
AWS resources by itself — you run the steps below once.

```text
GitHub Actions (master green)
  ├─ build, test ─► publish ghcr.io/leo-slv/ordercore-api (amd64 + arm64)
  └─ deploy-demo ── OIDC ─► IAM role ─► SSM SendCommand ─► EC2: /opt/ordercore/deploy.sh <sha>

EC2 t4g.small (Amazon Linux 2023, Elastic IP, security group: 80/443 only)
  docker compose (deploy/aws/compose.yml)
    caddy ── https://api.<ip-dashed>.sslip.io  ──► api:8080
          └─ https://mail.<ip-dashed>.sslip.io ──► mailpit:8025 (password)
    api · postgres · rabbitmq · mailpit
  systemd timer ─► backup.sh ─► pg_dump ─► S3 (kept 30 days)
```

## What you need

- An AWS account and the AWS CLI configured (`aws sts get-caller-identity`).
  Costs about US$ 18 a month while the stack exists.
- Docker locally (the parameters script uses it to hash the inbox password).
- A **Stripe test-mode** account (test keys only; the script refuses `sk_live_`).
- Optional: a Grafana Cloud free stack (`jq`, `yq` and `curl` for the import),
  a Resend account and a verified domain (to deliver real e-mail).

## 1. Make the image public (once)

The first green `master` publishes `ghcr.io/leo-slv/ordercore-api`. In
GitHub → the repository's Packages → `ordercore-api` → *Package settings* →
*Change visibility* → Public. The image holds no secret, and this lets the
server pull it without credentials. (The repository itself must be public too:
the server fetches `deploy/aws` from it at the deployed commit.)

## 2. Create the parameters

```bash
AWS_REGION=us-east-1 deploy/aws/scripts/create-parameters.sh
```

It generates the secrets (JWT key, database and broker passwords, the first
admin's password, the e-mail inbox password), asks for the optional settings
and writes everything under `/ordercore/demo/` in SSM Parameter Store. **Keep
the admin password and the inbox password it prints once.** Re-run it any time
to add a setting (existing ones are kept; `ROTATE=name1,name2` replaces
specific ones).

| Parameter | Type | Required | Read as |
|---|---|---|---|
| `jwt-signing-key` | secret | yes | `Jwt__SigningKey` |
| `postgres-password` | secret | yes | the database password |
| `rabbitmq-password` | secret | yes | `RabbitMq__Password` |
| `admin-email`, `admin-password` | string, secret | yes | `IdentitySeed__*` (first admin) |
| `mailpit-user`, `mailpit-password-hash` | string, secret | yes | the inbox's basic auth |
| `backup-bucket`, `public-ip` | string | created by the stack | backups; the address of the names |
| `storefront-url` | string | no | `Cors__AllowedOrigins__0` and the e-mail links (`/redefinir-senha`, `/confirmar-email`). Until set, the reserved `https://loja.<ip>.sslip.io` keeps the API valid |
| `stripe-secret-key`, `stripe-publishable-key`, `stripe-webhook-secret` | secret, string, secret | no (all three or none; none = the fake provider) | `Payments__Stripe__*` |
| `otlp-endpoint`, `otlp-headers` | string, secret | no | `OTEL_EXPORTER_OTLP_*` |
| `resend-api-key`, `mail-from` | secret, string | no | real e-mail instead of the inbox |

The fixed demo values (`AllowedHosts`, forwarded headers, `OpenApi__Expose`,
the Marfim e-mail brand, trace sampling 20 %) live in
`deploy/aws/scripts/render-env.sh`.

## 3. Create the stack

```bash
aws cloudformation deploy \
  --stack-name ordercore-demo \
  --template-file deploy/aws/ordercore-demo.yaml \
  --capabilities CAPABILITY_IAM \
  --parameter-overrides GitHubRepository=Leo-Slv/OrderCore
# add CreateGitHubOidcProvider=false if the account already has GitHub's OIDC provider
aws cloudformation describe-stacks --stack-name ordercore-demo --query 'Stacks[0].Outputs'
```

It creates the server, its Elastic IP, the security group (80 and 443 only),
the roles, the backup bucket (30-day lifecycle) and the role GitHub assumes.
The server's first boot installs Docker and deploys the current `master`; if
the parameters are missing or `master` has no published image yet, that first
deploy fails harmlessly and step 5 deploys it. Note the outputs: `ApiUrl`,
`MailInboxUrl`, `InstanceId`, `DeployRoleArn`.

## 4. Stripe webhook

In the Stripe dashboard (test mode) → Developers → Webhooks → add an endpoint
`<ApiUrl>/api/payments/webhooks/stripe`, with the events the API handles
(`payment_intent.amount_capturable_updated`, `.payment_failed`, `.succeeded`,
`.canceled`, `charge.refund.updated` and `charge.dispute.created`; see
`Docs/specs/payments/stripe-provider.md`). Copy its signing secret and re-run
`create-parameters.sh` (it asks for the keys and the secret), then step 5.
Without the webhook, outcomes only arrive through reconciliation (~15 min).

## 5. Let GitHub deploy

In the repository → Settings → Environments → create **demo**, with the
variables:

| Variable | Value |
|---|---|
| `DEMO_DEPLOY_ROLE_ARN` | the stack's `DeployRoleArn` |
| `DEMO_INSTANCE_ID` | `InstanceId` |
| `DEMO_API_URL` | `ApiUrl` |
| `DEMO_AWS_REGION` | the region (default `us-east-1`) |

From then on every green push to `master` runs `deploy-demo`: backup,
migrations, new version, health check; a failed migration leaves the running
version up, a version that never becomes healthy is replaced by the previous
one. Re-run the workflow to retry. Deploying by hand (on the server, through
SSM Session Manager): `sudo /opt/ordercore/deploy.sh <full commit sha>`.

## What the demo looks like

- **API docs**: `<ApiUrl>/scalar/v1` (public on the demo, `OpenApi__Expose`).
- **Payments**: Stripe test mode, with the test cards of `.env.example`.
- **E-mail**: nothing reaches real mailboxes unless a Resend key is set. The
  confirmation, password-reset and order e-mails land in `MailInboxUrl`
  (user from `mailpit-user`, the password printed in step 2). Everyone with
  that password sees every demo e-mail, links included — fine for test
  accounts, never for real customers. With Resend, store `resend-api-key` and
  `mail-from` (a domain verified there) and deploy again.
- **Storefront**: Marfim on Vercel with `NEXT_PUBLIC_API_URL=<ApiUrl>`; put its
  address in `storefront-url` (CORS and e-mail links) and deploy again. Marfim's
  `Docs/deploy/vercel.md` has its side.

## Observability

Create a Grafana Cloud stack, an OTLP token (Connections → OpenTelemetry) and
a service account token with the Admin role, then:

```bash
deploy/aws/scripts/create-parameters.sh   # otlp-endpoint and otlp-headers, then deploy
GRAFANA_URL=https://<stack>.grafana.net GRAFANA_TOKEN=<service account token> \
ALERT_EMAIL=you@example.com deploy/grafana/cloud/import.sh
```

The import loads the dashboards and alert rules (swapping the Prometheus
datasource uid) and sets an e-mail contact point as the default policy. Traces
are sampled by the API at 20 % (`Observability__TraceSamplingRatio`); the
collector's error/slow tail sampling (`deploy/otel`) is a local feature only.

## Operating it

- **Backups**: daily at 06:00 UTC and before every deploy, to
  `s3://<bucket>/postgres/<timestamp>-<label>.dump`, kept 30 days. List with
  `aws s3 ls s3://<bucket>/postgres/`.
- **Restore** (replaces the database): on the server,
  `sudo /opt/ordercore/current/scripts/restore.sh postgres/<file>.dump --yes`.
- **Rotate a secret**: `ROTATE=jwt-signing-key deploy/aws/scripts/create-parameters.sh`,
  then re-run the workflow (a new JWT key signs everyone out; a new database
  password also needs `ALTER USER ordercore PASSWORD '…'` — avoid).
- **A real domain**: point `api.` and `mail.` records at the Elastic IP and
  replace the names in `deploy/aws/Caddyfile` and `AllowedHosts` in
  `render-env.sh`; the rest does not change.
- **RDS or a managed broker later**: change the connection settings in
  `render-env.sh` and drop the container; the application does not change.
- **Tear down**: delete the stack (the backup bucket is retained: empty and
  delete it by hand), then the `/ordercore/demo/*` parameters.

## Risks

- **Let's Encrypt and `sslip.io`** share rate limits with everyone using that
  domain; if a certificate is refused, use `nip.io` or a cheap domain.
- **2 GB of memory** for five containers: limits are set in `compose.yml` and
  there is 1 GB of swap; watch `docker stats` after the first deploy.
- **A deploy stops the API for a few seconds** (single instance, accepted).
- **Data Protection keys** are not persisted: harmless for this JWT API, noted
  in the startup log.
