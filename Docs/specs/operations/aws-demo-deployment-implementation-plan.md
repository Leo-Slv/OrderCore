# Implementation plan — the OrderCore demo on AWS (V6)

Implements `Docs/specs/operations/aws-demo-deployment.md`. Staged as
usual: each stage goes through a `v6/<topic>` branch whose CI run must be
green before it is fast-forwarded into `master`, and leaves `dotnet build`,
`dotnet test` (all three projects) and `dotnet format --verify-no-changes`
passing. Nothing here creates AWS resources by itself: the user creates
the stack and the secrets (stage 6 runbook); CI deploys only once the
repository is configured to (stage 5).

## Shape

```text
GitHub Actions (master green)
  ├─ build & test ─► publish image ghcr.io/leo-slv/ordercore-api (amd64 + arm64, public)
  └─ deploy-demo ── OIDC ─► IAM role ─► SSM SendCommand ─► EC2: /opt/ordercore/deploy.sh <sha>

EC2 t4g.small (Amazon Linux 2023, arm64, Elastic IP, SG: 80/443 only)
  docker compose (deploy/aws/compose.yml)
    caddy ── https://api.<ip>.sslip.io ──► api:8080
          └─ https://mail.<ip>.sslip.io (basic auth) ──► mailpit:8025
    api (ASPNETCORE_ENVIRONMENT=Production, env rendered from SSM)
    postgres (EBS volume)   rabbitmq   mailpit
  systemd timer ─► backup.sh ─► pg_dump ─► S3 (30-day lifecycle)
  api ── OTLP/HTTP ──► Grafana Cloud (free)

SSM Parameter Store /ordercore/demo/*  (SecureString; created by the user)
```

## Stage 1 — The image and the demo switches

- **Multi-arch image.** `Dockerfile`: the SDK stage runs on the build
  platform (`FROM --platform=$BUILDPLATFORM`) and publishes for the target
  with `-a $TARGETARCH` (no QEMU-emulated `dotnet publish`); the runtime
  stage stays per target. CI's `build-push-action` gets
  `platforms: linux/amd64,linux/arm64` (+ `setup-qemu-action` only if a
  step still needs it). The GHCR package is made public once, by hand
  (runbook).
- **`OpenApi:Expose`** (default `false`): `Program.cs` maps the OpenAPI
  document and Scalar when Development **or** this setting is true. Still
  `AllowAnonymous`; the security headers' CSP, if any, must let Scalar
  load (checked by test).
- **OTLP to Grafana Cloud** needs no code: the SDK already reads
  `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`
  (`http/protobuf`) and `OTEL_EXPORTER_OTLP_HEADERS` (the auth header,
  from SSM). Trace sampling without a collector: `Observability:TraceSamplingRatio`
  (e.g. `0.2`) — error/slow tail sampling stays a collector feature, noted
  in the runbook.
- Tests: `OpenApiTests` gains "exposed outside Development when
  `OpenApi:Expose=true`" and "not exposed by default in Production".

## Stage 2 — The server's compose, Caddy and scripts (`deploy/aws`)

- `deploy/aws/compose.yml`: `caddy` (ports 80/443, volumes for certificates),
  `api` (image `ghcr.io/leo-slv/ordercore-api:${IMAGE_TAG}`, `env_file:
  api.env`, no published port), `postgres:16-alpine` (volume, password
  from the env file), `rabbitmq:4` (no management port published),
  `axllent/mailpit` (`MP_UI_AUTH_FILE`, `MP_MAX_MESSAGES=500`). Memory
  limits per service so 2 GB holds them all; health checks as in the local
  compose; `restart: unless-stopped`.
- `deploy/aws/Caddyfile`: `api.{$PUBLIC_IP_DASHED}.sslip.io` → `api:8080`;
  `mail.{$PUBLIC_IP_DASHED}.sslip.io` → `mailpit:8025` with `basic_auth`
  (bcrypt hash from SSM). Automatic HTTPS (Let's Encrypt).
- `deploy/aws/scripts/render-env.sh`: reads `/ordercore/demo/*` from SSM
  (`aws ssm get-parameters-by-path --with-decryption`) and writes
  `api.env` and `stack.env` (mode 600, root only), mapping each parameter
  to its setting (`Jwt__SigningKey`, `ConnectionStrings__OrderCoreDb`,
  `RabbitMq__*`, `Payments__Stripe__*`, `OTEL_EXPORTER_OTLP_*`, …) plus the
  fixed demo values: `ASPNETCORE_ENVIRONMENT=Production`,
  `AllowedHosts=api.<ip>.sslip.io`, `ForwardedHeaders__TrustAllProxies=true`
  (only Caddy can reach the API), `HTTPS_PORT=443`,
  `Cors__AllowedOrigins__0` and `Identity__Links__*` pointing at the
  reserved storefront name (`loja.<ip>.sslip.io`, not served yet),
  `Notifications__Smtp__Host=mailpit`, `Notifications__From`,
  `OpenApi__Expose=true`. Never echoes a value.
- `deploy/aws/scripts/deploy.sh <sha>`: fetch `deploy/aws` at `<sha>`,
  render the env, `backup.sh`, `docker compose pull`, run `migrate` with
  the new image (stop on failure, leaving the running version), `up -d`,
  wait for `GET /health/ready` through Caddy; non-zero exit on any failure.
- `deploy/aws/scripts/backup.sh`: `pg_dump -Fc` from the postgres
  container straight to `s3://<bucket>/postgres/<timestamp>.dump`;
  `restore.sh <key>` for the documented restore.
- `deploy/aws/systemd/ordercore-backup.{service,timer}`: daily backup.
- Checks in CI: `docker compose -f deploy/aws/compose.yml config`,
  `shellcheck` on the scripts, `caddy validate` on the Caddyfile (in a
  container).

## Stage 3 — Infrastructure as code (`deploy/aws/ordercore-demo.yaml`)

CloudFormation, parameters: `GitHubRepository` (`Leo-Slv/OrderCore`),
`AlertEmail`, optional `CreateGitHubOidcProvider` (an account has at most
one). Resources:

- Security group: inbound 80 and 443 only (no 22, no 5432/5672/15672).
- Elastic IP + EC2 `t4g.small`, Amazon Linux 2023 arm64 (AMI from the
  public SSM parameter), `gp3` 20 GB encrypted, IMDSv2 required.
- Instance role: `AmazonSSMManagedInstanceCore`, read of
  `/ordercore/demo/*` (+ `kms:Decrypt` on the AWS-managed SSM key),
  `s3:PutObject/GetObject/ListBucket` on the backup bucket only.
- S3 backup bucket: private, encrypted, versioning off, lifecycle delete
  after 30 days, `DeletionPolicy: Retain`.
- User data (bootstrap, idempotent): Docker + compose plugin, swap 1 GB,
  `/opt/ordercore`, the backup timer, then a first `deploy.sh` at the
  current `master`.
- GitHub OIDC provider (conditional) and a deploy role trusted only for
  `repo:Leo-Slv/OrderCore:ref:refs/heads/master` (environment `demo`),
  allowed `ssm:SendCommand` with `AWS-RunShellScript` on this instance
  and `ssm:GetCommandInvocation`.
- Outputs: public IP, API and Mailpit URLs, instance id, bucket, deploy
  role ARN.
- Validated in CI with `cfn-lint`; created by the user (runbook).

## Stage 4 — Secrets and the parameters script

- `deploy/aws/scripts/create-parameters.sh` (run by the user, locally,
  with their AWS credentials): generates the JWT key, PostgreSQL and
  RabbitMQ passwords, the admin password and the Mailpit inbox password
  (`openssl rand`), asks for the Stripe test keys, the Stripe webhook
  secret and the Grafana Cloud OTLP endpoint/token, and writes them as
  SecureStrings under `/ordercore/demo/`. Prints only the parameter names,
  and the admin and inbox passwords once, for the user to keep.
- `Docs/operations/aws-demo.md` lists every parameter, what reads it and
  how to rotate it (update the parameter, re-run the deploy).

## Stage 5 — Deploy from CI

- New job `deploy-demo` in `ci.yml`, after `publish-image`, on `master`
  only, in a GitHub environment `demo`, and only when the repository
  variable `DEMO_DEPLOY_ROLE_ARN` is set (so CI stays green before the
  stack exists): `aws-actions/configure-aws-credentials` with OIDC,
  `aws ssm send-command` running `/opt/ordercore/deploy.sh <sha>` on
  `DEMO_INSTANCE_ID`, wait for the result, print its output, fail on a
  non-zero exit; then `curl` `https://api.<ip>.sslip.io/health/ready`.
- `concurrency: demo-deploy` so two pushes never deploy at once.

## Stage 6 — Observability, runbook and docs

- **Grafana Cloud:** `deploy/grafana/cloud/import.sh` (run by the user
  with a Grafana Cloud API token) uploads the dashboards and the alert
  rules, rewriting the datasource uid (`prometheus` → the stack's
  Prometheus uid), and creates an e-mail contact point for `AlertEmail`
  on the default policy. The rule "API not reporting" works as is.
- **Runbook `Docs/operations/aws-demo.md`:** prerequisites (AWS account,
  AWS CLI, Grafana Cloud and Stripe test accounts), make the GHCR package
  public, create the parameters, create the stack, register the Stripe
  webhook at `https://api.<ip>.sslip.io/api/payments/webhooks/stripe`
  and store its secret, set the GitHub environment and variables, first
  deploy, what the demo looks like (Scalar, test cards, the Mailpit inbox),
  backup/restore, rotating secrets, swapping `sslip.io` for a real domain,
  moving PostgreSQL to RDS later, tearing everything down, cost.
- **Docs:** `Docs/operations/deployment.md` points at the AWS runbook;
  `README.md` (demo link, how to try it); `ORDERCORE_CONTEXT.md` (section
  41 Docker / deploy); `CLAUDE.md` (deploy files live in `deploy/aws`, no
  secret in them, CI deploys through SSM); execution notes.

## Risks to check during implementation

- **Let's Encrypt and `sslip.io`.** Certificates per name are rate
  limited; if `sslip.io` hits limits, use `nip.io` or register a cheap
  domain. Checked on the first deploy.
- **2 GB of memory** for five containers: limits sized during stage 2 and
  verified on the instance (`docker stats`); swap as a cushion.
- **The deploy gap.** Stopping the old API before the new one starts means
  seconds of downtime per deploy — accepted (spec, out of scope).
- **The demo inbox exposes every demo e-mail** to whoever has its
  password; registration stays open, so the demo must only ever hold
  test data.

## Commits

1. `build: multi-arch image; feat(api): OpenApi:Expose for the demo`
2. `feat(deploy): server compose, Caddy and deploy/backup scripts`
3. `feat(deploy): CloudFormation stack for the AWS demo`
4. `feat(deploy): SSM parameters script`
5. `ci: deploy the demo through SSM after a green master`
6. `feat(observability): Grafana Cloud import` and `docs: ...`
