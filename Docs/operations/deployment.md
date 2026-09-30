# Deploying OrderCore

How a new version goes out, whatever the hosting
(`Docs/specs/operations/production-readiness.md`, decision 1: not chosen
yet). The deploy is manual; CI only builds and publishes the image.

## What CI publishes

Every green push to `master` publishes the API image to the GitHub
Container Registry:

```text
ghcr.io/<owner>/ordercore-api:sha-<full commit sha>
ghcr.io/<owner>/ordercore-api:latest
```

Deploy by the `sha-…` tag, never by `latest`, so what runs is exactly one
commit and a rollback is choosing the previous tag. The image runs as a
non-root user and listens on HTTP port 8080; HTTPS is terminated in front
of it.

## Steps

1. **Back up PostgreSQL.** Every migration is forward-only; the backup is
   the way back if a migration goes wrong.
2. **Apply the migrations** with the new image, before the new version
   starts:

   ```bash
   docker run --rm <env or --env-file with the database settings> \
     ghcr.io/<owner>/ordercore-api:sha-<commit> migrate
   ```

   It applies the pending migrations of the eight module databases in a
   fixed order (Identity, Customers, Catalog, Inventory, Orders, Payments,
   AuditLogs, Messaging) and exits `0`. On the first failure it stops,
   leaves the remaining databases untouched and exits non-zero — **stop
   the deploy there** and fix (or restore) before trying again. Running it
   again only applies what is still pending. It needs only the database
   settings: no broker, no JWT key, and it starts nothing.
3. **Start the new version — one instance.** The outbox relay, the message
   consumers, the payment jobs and the real-time hub assume a single API
   instance (spec, decision 3); two instances would publish and run jobs
   twice. Stop the old one, start the new one (a short gap is expected),
   and wait for `GET /health/ready` to answer `200`.

Starting the API never applies migrations; `migrate` is the only way they
run outside development.

## Settings a deployment provides

Set as environment variables (`__` for `:`, e.g. `ConnectionStrings__OrderCoreDb`)
or the platform's secret store; none of them are committed.
`appsettings.Production.json` blanks the development defaults, and outside
Development the API **refuses to start** — naming the setting — when one
marked *checked* is missing or still local, or when Stripe is configured in
a way that would misbehave.

| Setting | Secret | Checked at startup | Notes |
|---|---|---|---|
| `ConnectionStrings__OrderCoreDb` | yes | required, not localhost | also what `migrate` uses |
| `Jwt__SigningKey` | yes | required, ≥ 32 bytes | |
| `RabbitMq__Host` | no | required | `RabbitMq__Port`, `__VirtualHost`, `__Username` as needed |
| `RabbitMq__Password` | yes | required | |
| `Cors__AllowedOrigins__0` (`__1`, …) | no | required, no localhost | the storefront's origin(s) |
| `AllowedHosts` | no | required, not `*` | host names the API answers for, `;`-separated |
| `ForwardedHeaders__KnownProxies__0` / `__KnownNetworks__0` | no | — | the TLS proxy in front; or `ForwardedHeaders__TrustAllProxies=true` when the API is only reachable through the platform's proxy |
| `HTTPS_PORT` | no | — | the public HTTPS port (usually 443), for redirecting plain HTTP |
| `IdentitySeed__AdminEmail` / `__AdminPassword` | password yes | — | first admin, created only if none exists |
| `Payments__Stripe__SecretKey` | yes | with the two below | empty = the fake provider |
| `Payments__Stripe__PublishableKey` | no | same mode as the secret key | |
| `Payments__Stripe__WebhookSecret` | yes | required with a secret key | from the webhook endpoint registered in Stripe's dashboard |
| `Payments__Stripe__AllowLiveKeys` | no | required for `sk_live_` keys | real money only on purpose |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | no | — | where traces, metrics and logs go |
| `RateLimits__<Policy>__PermitLimit` / `__Window` | no | — | only to change the defaults (`SignIn`, `SignUp`, `Refresh`, `Checkout`, `StripeWebhook`) |
| `Identity__Lockout__MaxFailedAttempts` / `__Duration` | no | — | only to change 5 / 15 minutes |

## Database users

`migrate` changes the schema, so it needs a user that can create and alter
tables. If the platform allows it, give the running API a separate user
with only read/write rights on the data, and use the schema-owning user
only for `migrate`.
