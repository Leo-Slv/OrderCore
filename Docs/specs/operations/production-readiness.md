# Production readiness (V4)

## What

OrderCore can be built, verified, deployed and run somewhere other than a
developer's machine, safely. V1–V3 built the features; nothing yet takes
them from a commit to a running environment, and a few defaults only make
sense locally. This closes the technical gaps found in the
production-readiness review:

1. **Continuous integration.** Every push and pull request builds the
   solution, runs the three test projects (the integration tests start
   their own PostgreSQL, RabbitMQ and stripe-mock containers) and checks
   formatting; a red build is visible before anything merges.
2. **Schema changes reach production deliberately.** The migrations of
   all eight module databases are applied by an explicit deployment step,
   never by the API at startup, in a known order, after a backup — and a
   failed migration stops the deploy instead of leaving the API running
   against a half-migrated schema.
3. **The edge is hardened.**
   - Abusive traffic is throttled: sign-in, sign-up, token refresh,
     checkout and the Stripe webhook have limits, and a client over the
     limit gets a clear `429` with a stable code and when to retry.
   - Repeated wrong passwords lock the account for a while, without
     revealing whether the account exists.
   - Behind a reverse proxy or load balancer the API sees the real
     client address and scheme (needed by the limits above, the logs and
     link generation), and only answers for the host names it is meant
     to serve.
   - HTTPS is enforced end to end, with the browser told to keep using it.
4. **The container runs with least privilege.** The API image runs as a
   non-root user.
5. **Misconfiguration fails fast.** A setting that would make the API run
   but misbehave stops it at startup with a message saying what to fix —
   notably Stripe configured without its webhook secret (payments would
   only be confirmed by reconciliation, minutes later), a live Stripe key
   where only test keys are expected (or the reverse), and the missing
   production settings (database, broker, allowed origins, host names).
6. **A production configuration exists.** Development defaults (a local
   connection string, `localhost` origins, 100% trace sampling) are not
   what a deployed environment runs with; the settings a deployment must
   provide are listed in one place, and none of them are committed.
7. **Technical records don't grow forever.** Sent outbox rows, handled
   inbox rows and resolved failed messages are removed after a retention
   period; the order timeline and the audit log, which are business
   history, are kept.
8. **Someone is told when something breaks.** Alert rules, versioned with
   the dashboards, fire when the API is down, events stop leaving the
   outbox, messages pile up in the failed list, or card declines spike.
9. **Responses carry the usual security headers**, and authenticated
   answers are never cached by browsers or proxies.
10. **Production samples traces** instead of keeping every one; failed
    requests are always kept.

## Why

- **The review found these as blockers**: without CI nothing guarantees a
  commit is sound; without a migration step the first deploy can't create
  the schema; without limits the authentication endpoints accept unlimited
  password guesses; running as root and silent misconfiguration turn small
  mistakes into incidents.
- **Payments raise the stakes.** With Stripe, a missing webhook secret or
  a live key in the wrong place costs money or leaves orders unconfirmed —
  the kind of error that should never make it past startup.
- **The identity spec deferred it.** Rate limiting of sign-in attempts was
  explicitly left out of V2; a public store can't keep it out.

## Out of scope

- Password recovery, e-mail and unpaid orders that hold stock — V5
  (`Docs/specs/identity/password-recovery.md`,
  `Docs/specs/orders/unpaid-order-expiry.md`).
- Business gaps: shipping and tax calculation, Pix in production, LGPD
  data export/erasure.
- Load testing and capacity planning.
- A web application firewall, DDoS protection or bot detection beyond the
  API's own limits (those belong to the hosting's edge).

## Decisions (resolved with the user)

1. **The hosting is not chosen yet, so V4 stays neutral.** The API ships
   as a container image; the migrations ship as a self-contained step any
   platform can run before the new version starts (Azure, AWS, a VPS or
   Kubernetes later, without redoing this); HTTPS is terminated by
   whatever sits in front of the API, which is why forwarded headers and
   allowed hosts matter.
2. **CI plus a published image, manual deploy.** Every push and pull
   request builds, runs the three test projects and checks formatting on
   GitHub Actions; on `master`, the API image and the migration step are
   also published to the GitHub Container Registry, ready for a deploy
   that stays manual until the hosting is chosen.
3. **One instance in V4.** The outbox relay, the consumer host, the
   payment jobs and SignalR keep assuming a single API instance; the
   deployment settings and the docs say so explicitly. Running several
   instances (row claiming or leader election, a SignalR backplane) is
   future work.
4. **Conservative, configurable limits, with account lockout.** Sign-in
   10 per minute per client address, and 5 wrong passwords in a row lock
   the account for 15 minutes; sign-up 5 per hour per address; token
   refresh 30 per minute per address; checkout 10 per minute per customer;
   the Stripe webhook 300 per minute. Over a limit: `429` with a stable
   code and a `Retry-After`. A locked account answers the same way as a
   wrong password, so lockout doesn't reveal which accounts exist.
5. **Live Stripe keys only on purpose.** A live secret key is refused at
   startup unless the configuration explicitly allows live keys; test
   keys are always accepted. The secret and publishable keys must be of
   the same mode, and a secret key without a webhook secret is refused.
6. **Retention.** Sent outbox rows and handled inbox rows: 30 days;
   resolved (replayed or discarded) failed messages: 90 days; the order
   timeline and the audit log are kept. All configurable.
7. **The extras from the review are in**: table cleanup, alert rules as
   code, security headers and production trace sampling (items 7–10).
8. **PayCore loses its version number.** Earlier specs called the
   extraction of Payments into PayCore "V4"; it becomes "PayCore
   (future)", since V4 and V5 are now production readiness.
