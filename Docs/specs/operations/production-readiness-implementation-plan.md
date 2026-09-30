# Implementation plan — production readiness (V4)

Implements `Docs/specs/operations/production-readiness.md`. Staged as
usual: each stage leaves `dotnet build`, `dotnet test` (all three
projects) and `dotnet format OrderCore.sln --verify-no-changes` passing
and ends in its own commit(s). The hosting is not chosen (decision 1), so
nothing here depends on a cloud provider: the deliverables are a
container image, a migration command in that image, configuration and
documentation.

## Shape

```text
push / PR ──► GitHub Actions: restore · build · format · unit · architecture · integration (Docker)
master    ──► + docker build ──► ghcr.io/<owner>/ordercore-api:{sha, latest}

deploy (manual, any platform):
  1. back up PostgreSQL
  2. docker run <image> migrate          ← applies the 8 contexts' migrations, exits non-zero on failure
  3. start the new API version (1 instance)

client ──► TLS proxy / load balancer ──► API (HTTP :8080, non-root)
             X-Forwarded-For/Proto        forwarded headers (trusted proxies only)
                                          host filtering · HSTS · security headers
                                          rate limits (429) · account lockout
```

## Stage 1 — Continuous integration

- `.gitattributes` (`* text=auto eol=lf`, binaries marked) and an
  `.editorconfig` with `end_of_line = lf`, so line endings are the same on
  Windows and on the Linux runner and `dotnet format` agrees on both — the
  mixed endings that `dotnet format` flagged during V3 came from
  `core.autocrlf` without any rule. The working copy is already LF, so
  renormalizing changes no content.
- `.github/workflows/ci.yml`, on every push and pull request:
  `actions/setup-dotnet` (the SDK from `global.json`, added if missing),
  NuGet cache, `dotnet restore`, `dotnet build --no-restore`,
  `dotnet format --verify-no-changes`, then the three test projects. The
  integration tests start their own containers (PostgreSQL, RabbitMQ,
  stripe-mock) through Testcontainers, which the Ubuntu runners support.
  Test results are uploaded as an artifact; a failing step fails the run.
- Concurrency group per branch, so a new push cancels the older run.

Verification: the workflow runs green on a branch before this stage is
committed on `master` (the first run is the test of the stage).

## Stage 2 — Migrations as an explicit step, least-privilege image

- **`migrate` command.** `dotnet OrderCore.Api.dll migrate` (and so
  `docker run <image> migrate`) builds the host's configuration and
  services without starting the web server, the jobs or the broker
  connection, applies every context's pending migrations in a fixed order
  (Identity, Customers, Catalog, Inventory, Orders, Payments, AuditLogs,
  Messaging), logs what it applied and exits `0`, or logs the failure and
  exits non-zero without touching the remaining contexts. Starting the API
  normally still never migrates (CLAUDE.md). A test runs the command
  against an empty PostgreSQL container and checks every context is at its
  latest migration, and that running it again is a no-op.
- **Dockerfile:** `USER $APP_UID` (the non-root user the .NET images ship
  with); nothing in the image needs root. The compose healthcheck keeps
  working.
- **Publish on `master`:** a second job in the workflow, after the tests,
  builds the image and pushes it to the GitHub Container Registry
  (`ghcr.io/<owner>/ordercore-api`, tags `sha-<commit>` and `latest`) with
  the workflow's own token (`packages: write`); nothing is published from
  pull requests.
- `Docs/operations/deployment.md` (new): the deploy runbook — back up, run
  `migrate`, start one instance — and what the database user needs (DDL
  rights for `migrate`, DML only for the API if the platform allows two
  users).

## Stage 3 — The edge

- **Forwarded headers** (`Shared/Presentation/Hosting`): `X-Forwarded-For`
  and `X-Forwarded-Proto` are honoured only from the proxies or networks
  listed in `ForwardedHeaders:KnownProxies`/`KnownNetworks`; for
  platforms where the API is only reachable through their proxy and its
  addresses aren't fixed, `ForwardedHeaders:TrustAllProxies=true` (off by
  default, documented as "only when the API can't be reached directly").
  First middleware in the pipeline.
- **Host filtering:** `AllowedHosts` comes from configuration; outside
  Development, `*` is refused at startup (stage 6).
- **HTTPS:** outside Development, `UseHsts` (one year, include subdomains)
  and `UseHttpsRedirection`, which see `https` through the forwarded
  headers; the API itself keeps listening on HTTP behind the proxy.
- **Security headers** (a small middleware in
  `Shared/Presentation/Security`): `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, and a
  restrictive `Content-Security-Policy` on API responses (not on the
  Development-only Scalar UI); `Cache-Control: no-store` on every response
  to an authenticated request.

Tests: a request with forwarded headers from an untrusted address keeps
the socket address; from a trusted one, the client address and scheme
change; the headers are present on API responses and `no-store` on an
authenticated one; HSTS only outside Development.

## Stage 4 — Rate limits

- ASP.NET Core's built-in rate limiter (`Microsoft.AspNetCore.RateLimiting`,
  no new package), policies registered by the module that owns the
  endpoint and applied with `[EnableRateLimiting]` on the actions:
  - Identity: `sign-in` 10/min per client address, `sign-up` 5/hour per
    address, `refresh` 30/min per address;
  - Orders: `checkout` 10/min per customer (from the token);
  - Payments: the Stripe webhook 300/min per address.
  Fixed windows, values under `RateLimits:<Policy>` (decision 4), the
  client address after the forwarded headers of stage 3.
- A rejected request answers `429` as `ProblemDetails` with code
  `too_many_requests` and a `Retry-After` header, through the same
  ProblemDetails pipeline as every other error; counted in a metric
  (`ordercore.http.rate_limited`, by policy) and logged without the
  address.
- Tests (integration, with low limits set through configuration): the
  n+1th request gets `429` with the code and `Retry-After`; a different
  address or customer isn't affected; the OpenAPI document declares `429`
  on the limited actions.

## Stage 5 — Account lockout

- `UserAccount` gains `FailedSignInCount` and `LockedOutUntil`
  (`RecordFailedSignIn(now, policy)`, `RecordSuccessfulSignIn(now)`,
  `IsLockedOut(now)`); the rule (5 failures in a row → locked for 15
  minutes, both configurable, `Identity:Lockout`) is a domain policy.
  Migration `AddAccountLockout` (Identity).
- `SignInUseCase`: a locked account answers exactly like a wrong password
  (`401 invalid_credentials`, same timing path — the password is still
  verified), so lockout reveals nothing; a correct password after the lock
  expires resets the count. Audited (`AccountLockedOut`) and measured.
  Refresh tokens of a locked account keep working (locking is about
  password guessing, not an active session); V5's password reset clears
  the lockout.
- Tests: unit tests of the policy (counting, expiry, reset); integration:
  five wrong passwords lock, the right one is then refused with the same
  answer, and accepted after the window (clock controlled).

## Stage 6 — Fail fast on misconfiguration, production settings

- **Stripe options validation** (`IValidateOptions<StripeOptions>`,
  `ValidateOnStart`), only when a secret key is set:
  - `sk_live_` refused unless `Payments:Stripe:AllowLiveKeys=true`
    (decision 5);
  - the publishable key must be of the same mode as the secret key;
  - the webhook secret is required.
- **Production settings validation** (outside Development): the
  connection string doesn't point to `localhost`, `Cors:AllowedOrigins`
  is set and has no `localhost`, `AllowedHosts` isn't `*`, the RabbitMQ
  host is set. Each failure names the setting to fix.
- `appsettings.Production.json` with the non-secret production defaults
  (logging levels, trace export settings from stage 8) and no connection
  string or origins, so a deployment must provide them.
- `Docs/operations/deployment.md` gains the table of every setting a
  deployment provides, which are secrets, and where they come from.
- Tests: each Stripe combination (live without permission, mixed modes,
  no webhook secret) stops the host with the message; valid test and
  allowed live configurations start; the production checks fail and pass
  as expected (host built with the `Production` environment).

## Stage 7 — Retention of technical records

- `RetentionBackgroundService` in Messaging (which owns the outbox, inbox
  and failed-message mechanics), daily: deletes sent outbox rows and inbox
  rows older than 30 days in every registered outbox source and inbox
  context, and failed messages replayed or discarded more than 90 days
  ago (decision 6) — in batches (`ExecuteDeleteAsync` with a limit), so a
  first run on a large table doesn't lock it. `Messaging:Retention`
  options; the order timeline and the audit log are never touched.
- Indexes needed by the deletes (`SentAt`, `ProcessedAt`, the failed
  messages' resolution time) where missing — migrations in the modules
  that own the tables.
- Metric of rows removed per table; a failing run is logged and retried
  the next day.
- Tests (integration, clock controlled): old sent rows go, recent ones and
  unsent ones stay; handled inbox rows the same; only resolved failed
  messages past 90 days go.

## Stage 8 — Alerts and trace sampling

- **Alert rules as code** in `deploy/grafana/alerting/` (provisioned into
  the LGTM container like the dashboards):
  - API not ready (`/health/ready` failing, from the up/health metrics);
  - outbox backlog growing or its oldest row older than 5 minutes;
  - failed messages waiting for someone;
  - card decline rate above normal; payment provider errors;
  - rate-limit rejections spiking (stage 4).
  The contact point comes from the environment (a webhook URL or e-mail),
  so no channel or secret is committed; without one, alerts show in
  Grafana only.
- **Trace sampling at the collector**: keeping every failed trace can only
  be decided when the trace has finished, so the API exports every span
  (`Observability:TraceSamplingRatio` stays 1.0) and an OpenTelemetry
  Collector configuration in `deploy/otel/` does tail sampling — every
  trace with an error or slower than a threshold, plus 10% of the rest
  (configurable). Used by the compose stack and meant for any production
  collector. Metrics and logs are not sampled.
- Verification: the rules load in Grafana; a stopped outbox relay fires
  its alert in the local stack; with the collector config, successful
  traces are sampled and failed ones always arrive (checked by hand in the
  local stack, as for the dashboards in the observability feature).

## Stage 9 — Docs

- `ORDERCORE_CONTEXT.md`: deployment and operations as they exist (the
  migrate step, one instance, the edge, limits and lockout, retention,
  alerts).
- `CLAUDE.md`: new endpoints get a rate-limit policy when they are
  anonymous or expensive; settings that can make the API misbehave get
  startup validation; `migrate` is the only way migrations run outside
  development; the one-instance assumption.
- Diagrams: `08-identity.md` (lockout), `09-messaging.md` (retention),
  `01-shared-kernel.md` (forwarded headers, security headers, rate-limit
  ProblemDetails), `06-payments.md` (Stripe options validation).
- `README.md`: CI badge, how to deploy (link to the runbook), the new
  `429 too_many_requests` code, lockout, the production settings.
- Execution notes appended to this plan.

## Commits

1. `ci: build, test and format on every push`
2. `feat(host): migrate command and non-root image` · `ci: publish the API image`
3. `feat(shared): forwarded headers, HTTPS and security headers`
4. `feat: rate limits on sign-in, sign-up, refresh, checkout and webhooks`
5. `feat(identity): account lockout`
6. `feat: fail fast on Stripe and production misconfiguration`
7. `feat(messaging): retention of technical records`
8. `feat(observability): alert rules and tail sampling`
9. `docs: ...`
