# Implementation plan — Asynchronous messaging

Implements `Docs/specs/events/async-messaging.md`. Adds one technical
module (`Messaging`), moves Payments' integration events onto RabbitMQ,
makes Orders and Inventory publish, and builds the order timeline from
the events. Staged like the previous features: each stage leaves
`dotnet build`, `dotnet test` (all three projects) and
`dotnet format OrderCore.sln --verify-no-changes` passing and ends in its
own commit(s).

## Decisions taken with the user while planning

(In addition to the seven in the spec.)

- **The `Contracts` rule is about messages.** A message handler may only
  depend on the publishing module's `Contracts/IntegrationEvents` (an
  architecture test enforces it). Synchronous adapters keep calling the
  owning module's Application layer, as today, until V4.
- **Retries:** 5 attempts — immediately, then after 10 s, 1 min, 5 min and
  30 min — before a message goes to the failed-message list. The schedule
  is fixed in code; only the test host shortens it (see Stage 1).
- **A new technical module, `Messaging`** (like `AuditLogs` and
  `Identity`), owns the RabbitMQ infrastructure, the outbox relay, the
  consumer host, the failed-message table and its admin endpoints.
  `Shared` only gets the abstractions every module uses.
- **The order timeline is admin-only.** Customers keep the status history
  they have; the customer experience is the Tracking feature's.
- **Stock alerts are published only**, on the change of state (entering
  low stock or running out), with no consumer yet.

## Shape (for reference in every stage)

```text
use case ── save ──► module DbContext ┬─ aggregate rows
                                      └─ <module>_outbox_messages   (same transaction)
                                              │
                          Messaging: OutboxRelay (polls every module's outbox)
                                              │  publisher confirms, then marks the row sent
                                              ▼
                              RabbitMQ topic exchange "ordercore.events"
                               routing key "<module>.<event>.v<version>"
                                              │
                         one queue per consumer: "<module>.<consumer>"
                                              │
                          Messaging: ConsumerHost ── inbox check ──► handler
                                              │ failure
                                              ▼
                 retry queues (10 s / 1 min / 5 min / 30 min, TTL + dead-letter back)
                                              │ 5th failure
                                              ▼
                              messaging.failed_messages (PostgreSQL)
                               list / replay / discard via backoffice
```

- **Envelope.** Every message carries `messageId` (the event id),
  `type`, `version`, `occurredAt`, `correlationId`, `causationId` (the
  message that caused it, if any) and the payload, as JSON.
- **Correlation id.** Read from the `X-Correlation-Id` request header (or
  generated) by a middleware, echoed in the response, stored with every
  outbox row, carried as a message header, and restored by the consumer
  host while it handles a message, so the events a handler publishes keep
  the same correlation id and name the handled message as their cause.
- **Outbox per module.** The outbox table lives in the publishing
  module's own `DbContext` — that is what makes "aggregate saved ⇔ event
  recorded" one transaction — with the module's name in the table name
  (all modules share one database). The relay reads every registered
  module's outbox.
- **Inbox per consuming module.** "Message X was handled by consumer Y"
  is recorded in the consuming module's `DbContext`, in the same save as
  the handler's changes; a unique `(MessageId, Consumer)` index makes a
  second delivery a no-op even when two arrive at once.

## Stage 1 — Messaging foundation

**Shared (abstractions only)** — `Shared/Application/Messaging`:
- `IntegrationEvent` (moved out of Payments; it stops implementing
  `IDomainEvent`): `EventId`, `Version`, `OccurredAt`.
- `IIntegrationEventHandler<TEvent>`.
- `IOutbox` — `Enqueue(IntegrationEvent)` into the current module's
  unit of work, and `ICorrelationContext` (`CorrelationId`,
  `CausationId`).
- `Shared/Infrastructure/Messaging`: the outbox and inbox persistence
  models and a `ModelBuilder` extension each module's `DbContext` calls
  to map them under its own table names; `OutboxWriter<TDbContext>`.
- `Shared/Presentation`: the correlation-id middleware.

**Modules/Messaging:**
- `Infrastructure/RabbitMq`: connection (the API refuses to start if
  RabbitMQ can't be reached after a short series of attempts —
  decision 6), topology declaration (exchange, consumer queues, retry
  queues), publisher with publisher confirms.
- `IntegrationEventRegistry`: every module registers the contract types
  it publishes (name + version ↔ CLR type) in its own
  `<Module>DependencyInjection`; consumers register handlers the same
  way.
- `OutboxRelayBackgroundService`: polls each registered outbox every
  second, publishes in `OccurredAt` order, marks a row sent only after
  the broker confirms; a publish failure is retried on the next poll and
  never stops the service.
- `ConsumerHostBackgroundService`: one channel per consumer queue,
  manual acknowledgements, a new DI scope per message, correlation
  restored, inbox checked and recorded, handler invoked; on an exception
  the message goes to the next retry queue (attempt count in a header)
  or, after the fifth attempt, into `failed_messages`, and is
  acknowledged — it never blocks the queue.
- `Infrastructure/Persistence`: `MessagingDbContext` with
  `failed_messages` (message id, type, consumer, payload, headers, last
  error, attempts, first/last failure, status `Pending`/`Replayed`/
  `Discarded`); migration `InitialMessagingSchema`.
- Configuration section `RabbitMq` (host, port, virtual host, user);
  the password comes from the environment/user-secrets like the JWT key.

**Local and tests:**
- `docker-compose.yml` gains `rabbitmq:4-management` (AMQP + management
  UI), credentials from `.env` (`RABBITMQ_USER`/`RABBITMQ_PASSWORD`);
  `.env.example` updated.
- `Testcontainers.RabbitMq`; `ApiDatabase` starts a RabbitMQ container
  next to PostgreSQL and points the host at it. The test host replaces
  the retry schedule with millisecond delays (same five attempts) so
  failure paths run in seconds.

Tests: envelope serialization round trip; retry routing (attempt n → the
right delay, 5th → failed); inbox makes a second delivery a no-op; relay
marks a row sent only after confirmation; a handler exception never stops
the host.

## Stage 2 — Payments over the broker

- Contracts move to `Modules/Payments/Contracts/IntegrationEvents`
  (`PaymentRequested`, `PaymentAuthorized`, `PaymentFailed`,
  `PaymentRefunded`) and gain **`PaymentCaptured`** and
  **`PaymentVoided`**, enqueued by `CapturePaymentUseCase` and
  `SettlePaymentForCancellationUseCase` in the same save as the payment.
- Payments' outbox becomes the shared one: migration renames
  `outbox_messages` to `payments_outbox_messages` and adds the
  correlation/causation columns; `OutboxPublisherBackgroundService`,
  `IntegrationEventTypeRegistry` and the Payments-local `IOutboxWriter`
  are removed.
- Orders' `PaymentAuthorized`/`PaymentFailed` handlers become
  `IIntegrationEventHandler<T>` consumed from the queue
  `orders.payment-outcomes`, with Orders' inbox
  (`orders_processed_messages`, migration in `OrdersDbContext`).
- Architecture test: a type implementing `IIntegrationEventHandler<T>`
  may reference another module only through its
  `*.Contracts.IntegrationEvents` namespace; nothing references a
  module's `IntegrationEvents` outside `Contracts`.

Tests: the storefront checkout still ends `Confirmed`/`PaymentFailed` —
now through RabbitMQ; capture and void enqueue their events.

## Stage 3 — Orders and Inventory publish

- **Orders** — `Modules/Orders/Contracts/IntegrationEvents`: order
  created, payment requested, confirmed, processing started, shipped,
  delivered, payment failed, cancelled. `EfOrderRepository.SaveChangesAsync`
  translates the aggregate's domain events into these and writes them to
  `orders_outbox_messages` in the same save (the in-process dispatch of
  domain events to the status history stays as it is).
- **Inventory** — `Modules/Inventory/Contracts/IntegrationEvents`:
  stock reserved, released, consumed, returned (with the order id), and
  a stock alert (`LowStock`/`OutOfStock`, product, available, reorder
  level) raised by `StockItem` only when its state changes into one of
  those. `InventoryUnitOfWork` writes them to `inventory_outbox_messages`
  in the same save.
- Migrations: `AddOrdersOutbox`, `AddInventoryOutbox`.

Tests: each transition enqueues exactly its event; a stock alert only on
the change of state (selling from 5 to 4 with a reorder level of 3 emits
nothing; 4 → 3 emits `LowStock`; 1 → 0 emits `OutOfStock`).

## Stage 4 — Order timeline

- Orders consumer `OrderTimelineProjector` (queue `orders.timeline`)
  subscribed to Orders' lifecycle, Payments' events and Inventory's
  reservation events; writes one row per event to `order_timeline`
  (order id, event id unique, type, source module, occurred at, a small
  details JSON — amount, reason, product/quantity). Migration
  `AddOrderTimeline`.
- `GetOrderTimelineUseCase` and `GET admin/orders/{id}/timeline`
  (`OrdersAdminController`), ordered by `OccurredAt`.

Tests: a checkout-to-delivery flow produces the full timeline in order,
once, even with a message delivered twice.

## Stage 5 — Failed messages in the backoffice

- `Messaging` use cases and `FailedMessagesController` (admin-only
  module, plain routes): `GET messaging/failed-messages?status=`,
  `GET messaging/failed-messages/{id}`, `POST …/{id}/replay`
  (republishes to the consumer's own queue with the attempt count
  reset; the row becomes `Replayed`), `POST …/{id}/discard`.
- Audit actions `FailedMessageReplayed`/`FailedMessageDiscarded`.

Tests: a message whose handler keeps failing ends in the list after five
attempts; replaying it once the cause is gone lets it through; a
discarded one is never retried.

## Stage 6 — End-to-end tests over HTTP and RabbitMQ

- The existing storefront, backoffice and flow tests keep passing on the
  broker.
- Correlation: one checkout's `X-Correlation-Id` appears on every event
  it caused, down to the confirmation.
- Duplicate delivery: republishing the same `PaymentAuthorized` changes
  nothing (order confirmed once, one timeline entry).
- Poison message → failed list → replay → processed.
- The API keeps accepting orders while RabbitMQ is briefly unavailable:
  events wait in the outbox and go out when it is back.

## Stage 7 — Docs

- New diagram `09-messaging.md`; updates to `01-shared-kernel`,
  `04-inventory`, `05-orders`, `06-payments` and the overview (the
  broker between modules).
- `ORDERCORE_CONTEXT.md`: sections 19–21 describe what now exists
  (broker, envelope, retries, inbox, contracts area), the module list,
  migrations.
- `CLAUDE.md`: the `Messaging` module, the `Contracts/IntegrationEvents`
  rule, "publish through the outbox, never directly", "every consumer is
  idempotent", correlation id.
- `README.md`: running with RabbitMQ (`.env`, management UI), the
  timeline and failed-message endpoints.
- Execution notes appended to this plan.

## Commits

One or more per stage, e.g.:

1. `feat(messaging): RabbitMQ foundation with outbox relay, inbox, retries and correlation id`
2. `feat(payments): publish over RabbitMQ; captured and voided events`
3. `feat(orders,inventory): publish order lifecycle, reservations and stock alerts`
4. `feat(orders): order timeline projected from events`
5. `feat(messaging): failed messages in the backoffice`
6. `test: messaging end to end over HTTP and RabbitMQ`
7. `docs: ...` (diagrams separately from the rest)
