# Implementation plan — Observability

Implements `Docs/specs/observability/observability.md`. Staged like the
previous features: each stage leaves `dotnet build`, `dotnet test` (all
three projects) and `dotnet format OrderCore.sln --verify-no-changes`
passing and ends in its own commit(s).

**Order of implementation:** Stage 2 (messaging traces and metrics)
builds on the `Messaging` module from
`Docs/specs/events/async-messaging-implementation-plan.md`, so Eventos is
implemented before this feature. Stages 1, 3, 4 and 5 don't depend on it.

## Decisions taken with the user while planning

(In addition to the seven in the spec.)

- **Dashboards are versioned.** Two Grafana dashboards live in the
  repository as code and are provisioned automatically: "Negócio"
  (orders, revenue, payments, checkout, inventory) and "API e
  mensageria" (latency, errors, outbox backlog, retries, failed
  messages).
- **Only ids reach logs and traces.** Order, payment, customer and
  product ids — never e-mail, name, address or document; never
  passwords, tokens or payment data.

## Stage 1 — OpenTelemetry wiring, trace id in responses, console logs

- `Shared/Infrastructure/Observability/ObservabilityExtensions`
  (`AddOrderCoreObservability`), called from `Program.cs`:
  - resource: `service.name = ordercore-api`, version, environment;
  - tracing: ASP.NET Core, `HttpClient` and Npgsql instrumentation (SQL
    text without parameter values), plus the `OrderCore.*` activity
    sources the modules and `Messaging` use; parent-based sampling,
    ratio from configuration (default: everything);
  - metrics: ASP.NET Core, `HttpClient`, runtime, Npgsql, plus the
    `OrderCore.*` meters;
  - logs: the OpenTelemetry logging provider (structured state and
    scopes included), so every log line carries its trace and span;
  - export over OTLP to the endpoint in the standard
    `OTEL_EXPORTER_OTLP_ENDPOINT` setting; with no endpoint configured
    (tests, a bare `dotnet run`) nothing is exported and nothing fails.
- Console: simple text formatter in Development, JSON formatter
  elsewhere.
- Responses: every `ProblemDetails` gets `traceId` (the current trace),
  and a middleware adds the `traceparent` header to every response.
- `docker-compose.yml`: `grafana/otel-lgtm` with a named volume for its
  data, OTLP ports for the API, Grafana on **port 3001** (the storefront
  uses 3000); the API service gets `OTEL_EXPORTER_OTLP_ENDPOINT`.
- Packages: `OpenTelemetry.Extensions.Hosting`,
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`,
  `OpenTelemetry.Instrumentation.AspNetCore`/`Http`/`Runtime`,
  `Npgsql.OpenTelemetry`.

Tests (in-memory exporter): a request produces a server span with the
database spans under it; an error response's `traceId` and the
`traceparent` header match that span.

## Stage 2 — Messaging traces and metrics (after Eventos)

- The outbox row already stores the writer's trace context (Eventos).
  The relay publishes inside a **producer** span linked to it; the
  consumer host handles each message inside a **consumer** span whose
  parent is the message's `traceparent`, following the OpenTelemetry
  messaging conventions (`messaging.system = rabbitmq`, destination,
  operation, message id). A retry is a new consumer span in the same
  trace, tagged with its attempt number.
- Meter `OrderCore.Messaging`: outbox backlog and age of the oldest
  unsent row per module (observable gauges), messages published and
  consumed (by type and consumer), retries (by attempt), failed messages
  (by consumer), handling duration.

Tests: one checkout is one trace from `POST orders/checkout` through the
relay and the Orders consumer to the confirmation save; a retried message
shows each attempt in that trace; the gauges report a backlog while the
broker is unavailable.

## Stage 3 — Business metrics and ids on spans and logs

- One `<Module>Metrics` class per module in its Application layer (the
  BCL's `System.Diagnostics.Metrics`, created through `IMeterFactory`;
  no dependency on OpenTelemetry there), recorded by the use cases:
  - Orders (`OrderCore.Orders`): orders created, confirmed, shipped,
    delivered, cancelled (`cancelled_by`), confirmed order value (per
    currency); checkout duration (by outcome) and refusals (by error
    code: `insufficient_stock`, `price_changed`, `address_not_found`…);
  - Payments (`OrderCore.Payments`): authorizations (approved/declined,
    decline reason), captures, voids, refunds; provider call duration
    (by operation and outcome);
  - Inventory (`OrderCore.Inventory`): reservations created, released
    and refused for lack of stock; low-stock/out-of-stock alerts.
- Use cases tag the current span with the ids they work on
  (`order.id`, `payment.id`, `customer.id`, `product.id`) and open a
  log scope with the same ids, so every log line of one order can be
  found by its id.
- The one log line that writes an e-mail today (the admin seed) logs the
  account id instead.

Tests: each metric is recorded once per event with the right tags
(`MeterListener`); a full checkout's logs, captured in the test host,
carry the order id and never the customer's e-mail or name.

## Stage 4 — Health checks

- `GET /health/live`: no dependency checks (the process answers).
- `GET /health/ready`: PostgreSQL and RabbitMQ reachable; answers only
  `Healthy`/`Unhealthy` (200/503), anonymously.
- `GET /health/details` (admin): each check's status and duration, plus
  the outbox backlog and pending failed messages from `Messaging`.
- `/health` stays as an alias of `live` (existing clients, the smoke
  test). The API container's compose health check uses `ready`.

Tests: `ready` is 503 while PostgreSQL or RabbitMQ is down and 200 when
both are up; `details` is admin-only and names the failing check.

## Stage 5 — Dashboards as code

- `deploy/grafana/` (new folder for infrastructure configuration, next
  to `docker-compose.yml`'s concerns; not application code): the two
  dashboards as JSON and the provisioning file, mounted into the
  `otel-lgtm` container by `docker compose`.
- "Negócio": orders per status over time, confirmed value, payment
  approval rate and decline reasons, checkout duration and refusals,
  stock alerts.
- "API e mensageria": request rate, latency percentiles and errors per
  endpoint, database time, outbox backlog/age, consumed/retried/failed
  messages, and a panel of recent error traces.

Checked by starting `docker compose` and generating traffic (checkout,
shipping, a refused payment); no automated test for dashboard JSON.

## Stage 6 — Docs

- `ORDERCORE_CONTEXT.md` section 30: what exists (signals, backend,
  conventions: span names, meters, tags, the ids-only rule).
- `CLAUDE.md`: instrumentation conventions for new code (use cases
  record their module's metrics and tag spans/log scopes with ids; never
  log personal data, secrets or payment data; new modules register their
  `OrderCore.<Module>` source and meter).
- `README.md`: running with observability (Grafana on 3001, where to
  find traces, logs, dashboards), the health endpoints, the trace id in
  responses.
- Diagram: `01-shared-kernel.md` (observability extensions, middleware)
  and the modules' metrics classes.
- Execution notes appended to this plan.

## Commits

1. `feat(observability): OpenTelemetry traces, metrics and logs with trace id in responses`
2. `feat(messaging): producer and consumer spans and messaging metrics`
3. `feat: business metrics and ids on spans and log scopes`
4. `feat(observability): live, ready and admin health details`
5. `feat(observability): Grafana dashboards as code`
6. `docs: ...`
