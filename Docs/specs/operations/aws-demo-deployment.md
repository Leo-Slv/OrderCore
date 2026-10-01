# Deploying the OrderCore demo on AWS (V6)

## What

OrderCore runs on AWS as a public **portfolio demonstration**: the API,
reachable over HTTPS, with everything it needs, at the lowest cost that
keeps it working and recoverable.

1. **One small server runs the whole backend.** The API, its database,
   its message broker, a web inbox for the demo's e-mails and the HTTPS
   proxy run together on a single ARM virtual machine (decisions 2–4). The
   architecture already assumes one API instance (V4), so nothing is lost.
2. **The infrastructure is code.** The server, its fixed public address,
   its permissions, the backup bucket and the deploy permissions for CI
   are described in a template in the repository and created once from
   it (decision 7). Nothing is created by clicking around that the
   repository doesn't describe.
3. **Secrets never touch the repository or the image.** Passwords, the
   JWT signing key and the Stripe and Grafana keys live in AWS's encrypted
   parameter store; the server reads them when it starts a version
   (decision 9).
4. **A green `master` reaches the server by itself.** After CI publishes
   the image, it asks AWS to update the server — backup, migrations, new
   version, health check — without SSH open to the internet (decision 8).
   A failed deploy leaves the previous version running and fails the CI
   run.
5. **The database is backed up.** Daily, and before every deploy, to
   object storage, keeping 30 days; restoring is a documented procedure
   (decision 3).
6. **The demo can be tried end to end.** Interactive API docs are public
   (decision 11), payments use Stripe's test mode with test cards
   (decision 12), and the demo's e-mails (confirmation, password reset,
   order e-mails) land in a password-protected web inbox instead of real
   mailboxes (decision 10), where a visitor finds the links they need.
7. **It is observed like production.** Traces, metrics and logs go to a
   free hosted Grafana, with the repository's dashboards and alert rules,
   alerts by e-mail (decision 13).
8. **It is cheap and predictable.** About US$ 18 a month on demand
   (estimate below), no load balancer, no managed broker, no NAT.

## Why

- **Every V before this made the code production-ready; nothing runs
  anywhere yet.** The production-readiness review's blockers (V4, V5) are
  closed; what is left is a place to run it.
- **A portfolio needs a live link**, and the cheapest AWS shape that
  still shows the real architecture (broker, outbox, jobs, real-time,
  e-mail, Stripe) is one server running the same containers as local
  development, behind HTTPS.
- **Managed pieces cost money even idle.** A managed RabbitMQ, a managed
  database and a load balancer would add several tens of dollars a month
  for a demo with no traffic. Each can replace its container later without
  changing the application — only configuration.
- **Recoverability and automation are what make it a deployment rather
  than a pet server**: infrastructure as code, secrets out of band,
  backups, and deploys from CI are the parts worth showing.

## Out of scope

- The Next.js storefront (decision 5) — its address is reserved in the
  configuration (CORS, the links in account e-mails) for when it ships.
- A real domain (decision 6) and real e-mail delivery (decision 10).
- Stripe live mode, real customers, any personal data beyond test accounts.
- High availability, a second instance, autoscaling, zero-downtime deploys
  (a short gap on each deploy is accepted).
- Managed database/broker (RDS, Amazon MQ), Kubernetes, Terraform.

## Decisions (resolved with the user)

1. **Region `us-east-1`** — the cheapest, with every service; latency to
   Brazil is acceptable for a demo.
2. **One EC2 `t4g.small`** (ARM, 2 GB) running Docker Compose: API,
   PostgreSQL, RabbitMQ, Mailpit and Caddy.
3. **PostgreSQL in a container** on the instance's disk, with a daily
   `pg_dump` (and one before each deploy) to an S3 bucket that keeps 30
   days. Restoring is manual and documented.
4. **ARM**: CI publishes the image for `linux/arm64` as well as
   `linux/amd64`.
5. **Only the API now**; the storefront comes later.
6. **No domain yet**: the API is reached through a free name that points
   at the server's fixed address (`sslip.io`, e.g.
   `api.<ip-with-dashes>.sslip.io`), only so that HTTPS certificates can be
   issued automatically; it is swapped for a real domain later.
7. **CloudFormation** template in the repository (`deploy/aws`) creates the
   AWS resources.
8. **Deploys from CI through AWS Systems Manager** (no SSH port open), with
   a role GitHub assumes through OIDC — no AWS key stored in GitHub.
9. **Secrets in SSM Parameter Store** (SecureString, standard tier).
10. **E-mail stays on the server**: Mailpit, with its web inbox published
    behind a password. Nothing is delivered to real mailboxes. Everyone who
    has the inbox password sees every demo e-mail, links included — fine for
    test accounts, never for real customers.
11. **Interactive API docs (Scalar and the OpenAPI document) are public on
    the demo**, by a setting that is off by default outside Development.
12. **Stripe in test mode**, with its webhook registered in the Stripe
    dashboard against the demo's address.
13. **Grafana Cloud's free tier** for traces, metrics and logs, with the
    repository's dashboards and alert rules; alerts by e-mail.
14. **The image on GHCR is public** (it holds no secret), so the server
    needs no registry credential. The repository is public too, so the
    server fetches the deploy files from it at the deployed commit.

## Estimated cost (us-east-1, on demand, to confirm in the AWS calculator)

| Item | Per month |
|---|---|
| EC2 `t4g.small` | ~US$ 12.30 |
| Public IPv4 address (Elastic IP, charged since 2024) | ~US$ 3.60 |
| EBS `gp3` 20 GB | ~US$ 1.60 |
| S3 backups, SSM, data transfer at demo volume | < US$ 1 |
| Grafana Cloud free, Stripe test mode, GHCR, GitHub Actions | US$ 0 |
| **Total** | **~US$ 18** |

New AWS accounts may have credits or a free tier that lowers this; it is
not counted on.
