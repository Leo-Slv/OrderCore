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

## Database users

`migrate` changes the schema, so it needs a user that can create and alter
tables. If the platform allows it, give the running API a separate user
with only read/write rights on the data, and use the schema-owning user
only for `migrate`.
