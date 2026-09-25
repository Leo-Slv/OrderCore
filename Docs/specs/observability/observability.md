# Observability (V3, "Observabilidade")

## What

Anyone running OrderCore can follow a request through every module, the
database and the broker, see how the system and the business are doing,
and tell whether it is ready to serve — without reading code or guessing
from scattered console lines (`ORDERCORE_CONTEXT.md` section 30).

1. **Distributed tracing.** One trace per request, following it through
   the API, the database calls, the outbox, RabbitMQ and every consumer
   the message reaches, including the events those consumers publish —
   so the path "checkout → payment authorized → order confirmed" is one
   trace even though it crosses an asynchronous boundary (depends on the
   Eventos feature). Traces are viewable locally in a UI.
2. **Structured logs.** Logs are structured (key/value, not only text),
   carry the trace they belong to, and name the business entities
   involved (order, payment, product, customer) so every log line of one
   order can be found. A log line can be opened from its trace and a
   trace from a log line.
3. **Metrics.**
   - technical: HTTP request rate, latency and errors per endpoint;
     database and runtime; messaging (outbox backlog and age, messages
     published/consumed, retries, failed messages);
   - business: the figures that say whether the shop is working (see the
     open decision on which).
4. **Health.** A liveness check (the process is up) and a readiness
   check (it can serve: PostgreSQL and RabbitMQ reachable), so an
   orchestrator or load balancer can tell "restart me" from "don't send
   me traffic yet".
5. **Errors can be traced back.** Every error response names the trace
   it belongs to, so a customer's or frontend's report ("I got an error
   at checkout") leads straight to the trace and its logs.
6. **Runs locally.** `docker compose` brings up the backend(s) that
   receive and show traces, metrics and logs; the API sends them there
   using an open standard, so a different backend later is a
   configuration change, not a code change.

## Why

- **It is the project's stated next phase** (README "Evolução planejada":
  "Resiliência e observabilidade", right after asynchronous messaging)
  and V3 of the frontend plan, and section 30 already lists what is
  expected: structured logging, correlation id, trace id, event id,
  order id, payment id, metrics, distributed tracing, health checks.
- **Asynchronous flows are opaque without it.** Once events go through
  RabbitMQ with retries and dead-lettering, "why is this order still
  PendingPayment?" can only be answered by following the message.
  Today there is nothing to follow.
- **PayCore (V4) makes it mandatory.** Section 30's goal is one trace
  from `POST /orders` through RabbitMQ, PayCore and Stripe and back; that
  has to work inside one process first.
- **Readiness is a real gap.** `/health` answers 200 even with the
  database down, so nothing can tell a broken instance from a working
  one.

## Out of scope

- Alerting rules and on-call notifications (dashboards and data first).
- Production hosting of the observability backends (only local, via
  `docker compose`).
- Frontend (browser) telemetry.
- Log retention policies and PII scrubbing beyond not logging secrets,
  passwords, tokens or full payment data (which must never be logged).

## Decisions (resolved with the user)

1. **OpenTelemetry.** Traces, metrics and logs are emitted with
   OpenTelemetry and exported over OTLP — vendor neutral, what .NET emits
   natively, and what a trace crossing into PayCore (V4) needs.
2. **Grafana LGTM, persisted.** Locally, `docker compose` runs the
   Grafana stack (Tempo for traces, Loki for logs, Prometheus/Mimir for
   metrics, Grafana to explore them and hold dashboards) with its data
   on a Docker volume, so history survives restarts. Telemetry is not
   stored in the application's own database: its volume is far larger
   than the business data's, and the system being watched shouldn't
   hold its own monitoring.
3. **The trace id is the correlation id.** One id for one journey:
   messages carry the standard W3C trace context, and whatever the
   system calls "correlation id" is the trace id. The Eventos plan's own
   `X-Correlation-Id` is dropped (its spec and plan are updated).
4. **Business metrics:** orders (created, confirmed, shipped, delivered,
   cancelled — by who cancelled — and the value of confirmed orders),
   payments (authorizations approved/declined by reason, captures, voids,
   refunds, provider response time), checkout (duration and refusal
   reasons: insufficient stock, price changed, address...) and inventory
   (reservations created/released, reservations refused for lack of
   stock, low-stock/out-of-stock alerts).
5. **Health:** `live` (the process is up) and `ready` (PostgreSQL and
   RabbitMQ reachable) answer only healthy/unhealthy to anyone; the
   detail of each check (which one failed, how long it took, the outbox
   backlog) is admin-only.
6. **Logs:** readable text on the console in Development, structured
   JSON elsewhere; the full structured logs always go to the backend
   through OpenTelemetry regardless of the console format.
7. **Trace id in responses:** every error (`ProblemDetails`) carries its
   `traceId`, and every response carries the trace context in a header,
   so the frontend can attach it to any report, not only errors.
