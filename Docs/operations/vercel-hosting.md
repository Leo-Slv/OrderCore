# Hosting OrderCore on Vercel

Runbook for `Docs/specs/operations/vercel-hosting.md`. Vercel runs **containers**
(beta) in two ways, and OrderCore needs both:

| Piece | Vercel product | Built from | Does |
|---|---|---|---|
| **web** | container Function | `Dockerfile.vercel` (`Hosting:Role=Web`) | answers HTTP: the whole API |
| **worker** | Daemon | `Dockerfile` + `command` in `vercel.json` (`Hosting:Role=Worker`) | the background services: outbox relay, RabbitMQ consumers, e-mail dispatcher, payment and reservation jobs |

A Function only lives while it has traffic (it scales to zero after 5 minutes)
and a Daemon cannot receive HTTP, so one process can't be both. The two
pieces share the project's environment variables. PostgreSQL and RabbitMQ are
**not** hosted by Vercel: use external services (below).

```text
Marfim (Vercel project "marfim") ──► OrderCore web (Vercel project "OrderCore", Function)
                                              │ writes outbox rows              ▲ Stripe webhooks
                          Neon PostgreSQL ◄───┴───────────────► OrderCore worker (Daemon, 1 instance)
                                                                       │ relay / consumers
                                                               CloudAMQP RabbitMQ
```

## Limits to know before you start

- **Beta**: Vercel Container Runtime and Daemons are in beta; behavior and
  quotas can change.
- **Hobby quota**: daemons get 10 Active CPU-hours per month per team; at the
  limit Vercel stops the worker until next month (events stop flowing: no
  e-mails, no order progress after payment). A mostly idle worker uses little.
- **Daemons only run in production deployments**, as one `amd64` instance
  (OrderCore's background services assume a single instance).
- **Cold starts**: the first request after 5 idle minutes starts the container
  (a few seconds).
- **No real-time tracking**: the SignalR hub (`/hubs/...`) can't receive pushes
  from the worker (no backplane). The Marfim storefront doesn't use it.
- **E-mail**: no Mailpit here. Only Resend (or an external SMTP) works. A
  development Resend key without a verified domain delivers only to the e-mail
  of your Resend account, from `onboarding@resend.dev`, so other people don't
  get confirmation e-mails (and checkout needs a confirmed e-mail).
- **Migrations are manual** (step 3): nothing runs them on Vercel.

## 1. PostgreSQL (Neon)

Create a Neon project (free plan) in the region closest to `iad1`
(us-east-1). From the connection details copy the **direct** (non-pooled)
connection and write it as an Npgsql string:

```text
Host=<endpoint>.neon.tech;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Maximum Pool Size=5
```

(`Maximum Pool Size` keeps many Function instances under Neon's connection
limit.)

## 2. RabbitMQ (CloudAMQP)

Create a CloudAMQP instance (the free "Little Lemur" plan is enough), in the
same region. Note its host, user (also the virtual host), password. Connect
over TLS: `RabbitMq__Port=5671`, `RabbitMq__Tls=true`.

## 3. Create the schema (from your machine)

```bash
docker run --rm -e ConnectionStrings__OrderCoreDb="<the Npgsql string>" \
  ghcr.io/leo-slv/ordercore-api:latest migrate
# optional, the Marfim storefront's demo catalog:
docker run --rm -e ConnectionStrings__OrderCoreDb="<the Npgsql string>" \
  ghcr.io/leo-slv/ordercore-api:latest seed-demo
```

(Use the real variable name `ConnectionStrings__OrderCoreDb`. The image is
public after `Docs/operations/aws-demo.md` step 1; otherwise run
`dotnet run -- migrate` from the repository with that variable set.) Repeat
`migrate` after every release that adds a migration, before it goes live.

## 3b. Secrets to generate

JWT signing key (`openssl rand -base64 48`) and the first admin's password
(8+ characters with a letter and a digit).

## 4. The Vercel project

Import the `Leo-Slv/OrderCore` repository (root directory `./`, application
preset **Container**), production branch `master`. Environment variables
(Production; the web and the worker read the same ones):

| Variable | Value |
|---|---|
| `PORT` | `8080` (must match the image) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__OrderCoreDb` | the Npgsql string (step 1) |
| `Jwt__SigningKey` | the generated key |
| `RabbitMq__Host`, `RabbitMq__Port`=`5671`, `RabbitMq__Tls`=`true`, `RabbitMq__VirtualHost`, `RabbitMq__Username`, `RabbitMq__Password` | from CloudAMQP |
| `Cors__AllowedOrigins__0` | `https://marfim-kappa.vercel.app` |
| `AllowedHosts` | the API's own host, e.g. `ordercore-xyz.vercel.app` (set after the first deploy shows it; the API refuses `*`) |
| `ForwardedHeaders__TrustAllProxies` | `true` (only Vercel's proxy reaches it) |
| `Identity__Links__ResetPassword` | `https://marfim-kappa.vercel.app/redefinir-senha?token={token}` |
| `Identity__Links__ConfirmEmail` | `https://marfim-kappa.vercel.app/confirmar-email?token={token}` |
| `IdentitySeed__AdminEmail`, `IdentitySeed__AdminPassword` | the first admin (created by the worker) |
| `Notifications__Resend__ApiKey` | your Resend key (`re_...`) |
| `Notifications__From` | `Marfim <onboarding@resend.dev>` (a verified domain, if you have one) |
| `Notifications__Brand__Name` / `__Wordmark` / `__WordmarkAccent` / `__AccentColor` / `__AccentSoftColor` / `__Footer` | `Marfim` / `marfim` / `.` / `#3b3fd9` / `#ececfd` / the footer text |
| `Payments__Stripe__SecretKey`, `__PublishableKey`, `__WebhookSecret` | Stripe **test** keys (step 6) |
| `OpenApi__Expose` | `true` to show Scalar/OpenAPI publicly |
| `Messaging__RelayPollInterval` | optional, `00:00:10`: how often the worker looks for unsent events (default `00:00:01`); raise it to save the Daemon CPU quota and database wake-ups, at the cost of up to that much delay per event |
| `Notifications__Dispatcher__Interval` | optional, `00:00:30`: how often queued e-mails are sent (default `00:00:05`) |

Deploy. The build creates the web Function and the `worker` Daemon (it is
listed under the project's Daemons; check its logs for "Connected to
RabbitMQ ... consumer queue(s) declared"). Open `<api>/health/ready`: `200`
means PostgreSQL and RabbitMQ are reachable.

## 5. Marfim

In the Marfim project set `NEXT_PUBLIC_API_URL=https://<the API's host>` (its
CSP is built from it) and redeploy. Optionally `ORDERCORE_API_URL` for the
server routes.

## 6. Stripe webhook

Stripe dashboard (test mode) → Webhooks → `https://<the API's host>/api/payments/webhooks/stripe`,
with the events in `Docs/operations/aws-demo.md` step 4; put its signing secret
in `Payments__Stripe__WebhookSecret` and redeploy.

## Operating it

- **Logs**: the project's Logs, filtered by the web Function or the daemon.
- **Deploying a new version**: push to `master`; a new production deployment
  replaces the Function and rolls the worker (30 s graceful stop; events
  in flight are retried, handlers are idempotent). Run `migrate` first when the
  release has migrations.
- **Stopping the worker**: set its instance count to 0 from the dashboard.
- **Changing the role of an image**: it is `ENV Hosting__Role` in
  `Dockerfile.vercel` (Web) and the `command` of the daemon (Worker); `All`
  (the default everywhere else) runs both in one process.
