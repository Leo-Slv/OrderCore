# Asynchronous messaging (V3, "Eventos")

## What

The events that cross module boundaries travel through a real message
broker instead of being dispatched in-process, with the delivery
guarantees that implies, and the order's history across modules becomes
visible as one timeline.

1. **Real broker.** Integration events written to a transactional outbox
   are published to a message broker (RabbitMQ — `ORDERCORE_CONTEXT.md`
   sections 20 and 21) and consumed from it, in the same process for
   now. Publishing no longer happens by calling the in-process
   dispatcher.
2. **Reliable delivery.** Delivery is at least once. A message that can't
   be published or handled right now is retried with growing delays; one
   that keeps failing is set aside (dead-lettered) where it can be
   inspected, and it never blocks the messages behind it. A failure while
   publishing or handling never stops the application.
3. **Idempotent consumers.** Every consumer recognises a message it has
   already handled and does nothing the second time (section 21: messages
   may arrive more than once, late, or out of order). Consumers already
   tolerate state that has moved on (a late `PaymentAuthorized` for a
   cancelled order); this adds protection against the same message twice.
4. **Stable message contracts.** Events carry an id, a type, a version,
   when they happened, and the trace context (the correlation id is the
   trace id — decided in `Docs/specs/observability/observability.md`)
   tying together everything one request caused (the checkout that led to the authorization that led to
   the confirmation). A consumer depends on the message contract, not on
   the publishing module's types — which is what lets Payments leave the
   process later (V4, PayCore) without its consumers changing.
5. **More modules publish.** Besides Payments, Orders and Inventory
   publish the changes others need to react to or show (decision 3).
6. **Order timeline.** The admin order detail shows one timeline of what
   happened to the order across modules — order created, stock reserved,
   payment authorized/captured/voided, order confirmed, shipped,
   cancelled — in the order it happened, as the frontend plan's
   "Timeline" shows. Today the pieces exist but are scattered (the order's
   status history, and audit entries filed under the payment or the
   reservation, not the order).
7. **Runs locally and in tests.** `docker compose` brings the broker up
   with the rest; integration tests run against a real broker, the same
   way they already run against a real PostgreSQL.

## Why

- **It is the next step of the project's own roadmap** (README "Evolução
  planejada": events and asynchronous communication → idempotent
  consumers → resilience and observability → PayCore) and V3 of the
  frontend plan. The outbox was always meant to feed RabbitMQ; the
  in-process dispatch was documented as a temporary bridge.
- **Today's bridge is fragile.** One handler throwing inside the outbox
  publisher ends the background service — and, with .NET's default
  behavior, the whole API. There is no retry policy, no place for a
  poisoned message to go, and no protection against handling a message
  twice if the process dies between handling and marking it done.
- **PayCore needs it.** Payments can only move to its own service once
  what it tells Orders (and what Orders asks of it) goes over the wire
  with contracts both sides own independently.
- **Tracking and Observabilidade build on it.** Real-time tracking (V3,
  next) pushes the status changes these events carry, and distributed
  tracing is only meaningful once there is an asynchronous boundary to
  trace across.
- **The timeline is the best demonstration of the architecture** the
  frontend plan asks for: the order's life across modules, not a CRUD
  table.

## Out of scope

- Extracting Payments into PayCore (V4).
- Pushing updates to the browser (V3 "Tracking", its own feature).
- OpenTelemetry tracing, metrics and dashboards (V3 "Observabilidade"),
  beyond carrying a correlation id in every message.
- A real payment provider (V3 "Pagamentos", Stripe).
- Running consumers in a separate process/container: they stay in the
  API host.
- New sagas beyond the existing checkout/cancellation sequences.

## Decisions (resolved with the user)

1. **The official RabbitMQ client, no messaging framework.** The outbox
   relay, retries with growing delays, dead-lettering and the record of
   already-handled messages are written in the project, where they can
   be read and tested — which is the point of the project — and nothing
   commercially licensed is involved.
2. **Only events go over the broker in this feature.** What modules
   *announce* travels through RabbitMQ; what Orders *asks* of Payments
   (request payment, capture on shipping, settle on cancellation) stays
   a synchronous call answering with the outcome. Turning those requests
   into messages belongs to V4, when Payments becomes PayCore.
3. **Events published:**
   - Payments: the existing requested/authorized/failed/refunded, plus
     **captured** and **voided**;
   - Orders: the **order lifecycle** — created, payment requested,
     confirmed, processing, shipped, delivered, payment failed,
     cancelled;
   - Inventory: **reservations** — reserved, released, consumed,
     returned — and **stock alerts** when a product becomes low on stock
     or runs out.
4. **The order timeline is a projection of the events.** A consumer
   files every event about an order into the order's timeline, in the
   order the events happened; the admin order detail reads it. The audit
   log stays what it is (who did what), separate from the business
   history.
5. **Failed messages are handled in the backoffice.** Messages that
   exhausted their retries can be listed (type, error, attempts, when)
   and replayed or discarded through admin endpoints.
6. **Always the real broker.** One code path: the API needs RabbitMQ to
   start, `docker compose` provides it, integration tests start a
   RabbitMQ container like they do PostgreSQL, and unit tests use fakes.
   The in-process dispatch of integration events is removed.
7. **Message contracts live in the publishing module**, in a public
   `Contracts/IntegrationEvents` area that is the only part of a module
   other modules may reference (enforced by an architecture test). For
   PayCore (V4) that area becomes Payments' contract package.
