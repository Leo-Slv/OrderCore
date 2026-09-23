# Payment processing and the transactional outbox

## What

Implement the Payments module per
[06-payments.md](../../diagrams/implementation-class/06-payments.md):

- Expand the existing `Payment` aggregate (`Provider`, `CustomerPaymentMethodId`,
  `CreatedAt`/`UpdatedAt`/`AuthorizedAt`/`CapturedAt`, `Refunds`) and add
  the `Refund` child entity (`RequestRefund`, `Complete`, `Fail`), per the
  diagram's "Invariante a confirmar" note: the refundable-balance check
  lives in `Payment.RequestRefund`, not the use case.
- `IPaymentRepository` and the six use cases: `CreatePaymentUseCase`,
  `AuthorizePaymentUseCase`, `CapturePaymentUseCase`, `FailPaymentUseCase`,
  `RequestRefundUseCase`, `GetPaymentByOrderIdUseCase`.
- The Transactional Outbox: `OutboxMessage`, `IOutboxWriter`/`OutboxWriter`,
  `OutboxPublisherBackgroundService`. `PaymentRequested`/`PaymentAuthorized`/
  `PaymentFailed`/`PaymentRefunded` (already existing placeholder types)
  become real: enqueued transactionally alongside the `Payment` write,
  then actually published.
- `PaymentWebhookHandler` — the entry point a real provider's async
  callback would hit; today invoked directly by tests/manual calls, since
  `FakePaymentProvider` resolves synchronously and there is no public HTTP
  webhook endpoint in this diagram to route it from a real request.
- `PaymentsController`, Requests/Responses, `PaymentPresenter`.
- Finishes Orders' checkout flow (`Docs/specs/orders/checkout-aggregate.md`):
  `IPaymentGateway`/`PaymentGatewayAdapter`, `RequestOrderPaymentUseCase`,
  `OrdersController.RequestPaymentAsync`, and
  `PaymentAuthorizedIntegrationEventHandler`/`PaymentFailedIntegrationEventHandler`.

## Why

Payments is called out as the module needing the most deliberate design
in the project (sections 13-21): idempotency, a state machine instead of
a boolean "paid" flag, and — the reason this spec exists now instead of
later — the mechanism that finally lets Orders react to a payment
succeeding or failing without polling. Until this feature, `IProductCatalog`/
`IInventoryService` were the only adapters Orders had that actually did
anything; `IPaymentGateway` was the missing third leg of the checkout
flow described in 05-orders.md's own "Fluxo de checkout" note.

## How "publish" works before RabbitMQ exists

Section 21 (RabbitMQ) is explicitly a later phase — "entra quando um
módulo passar a depender de fato dele", not before. `OutboxPublisherBackgroundService`
still needs to make `PaymentAuthorized`/`PaymentFailed` reach Orders'
integration-event handlers, so it publishes by calling the *existing*
`IDomainEventDispatcher.DispatchAsync` (Shared kernel) directly, the same
one `InventoryUnitOfWork`/`EfOrderRepository` call for in-process domain
events. This means the `IntegrationEvent` base record needs to satisfy
`IDomainEvent` (it already has `EventId`/`OccurredAt` — only `Version`
doesn't map). This is a deliberate, temporary bridge: it reuses existing
shared-kernel plumbing instead of inventing a second dispatcher for a
transport that doesn't exist yet, without pretending an integration event
*is* a domain event conceptually (still cross-process in intent, still
routed through the outbox table, still logged with its own `Version` for
schema evolution) — it only shares the technical delivery mechanism until
RabbitMQ actually replaces it.

## Decisions (not asked as open questions — reasoned from the diagram/spec precedent, documented for the record)

- **`CreatePaymentUseCase` authorizes synchronously in the same call**:
  it creates the `Payment`, calls `IPaymentProvider.AuthorizeAsync`
  immediately (the only provider is `FakePaymentProvider`, which is
  synchronous/in-process — there's no async webhook round-trip to wait
  for), marks the payment `Authorized`/`Failed` based on the result, and
  enqueues `PaymentAuthorized`/`PaymentFailed` accordingly.
  `AuthorizePaymentUseCase` is a separate, explicit retry path for a
  payment stuck in `Pending`/`Processing` (e.g. the provider call itself
  failed/timed out before `CreatePaymentUseCase` could record an
  outcome) — not something `CreatePaymentUseCase` calls internally.
- **`CapturePaymentUseCase` enqueues nothing**: only `PaymentRequested`/
  `PaymentAuthorized`/`PaymentFailed`/`PaymentRefunded` exist as
  integration events — there is no `PaymentCaptured` event, so capture
  stays a Payments-internal state change Orders never needs to react to.
- **`Payment.Refund()` marks the whole payment `Refunded` regardless of
  whether the specific `Refund` was partial or full** — `PaymentStatus`
  has no `PartiallyRefunded` value, matching the diagram exactly as
  given; a future feature can split that out if partial-refund reporting
  ends up mattering.
- **The outbox and the payment write commit in the same transaction**:
  `OutboxWriter.Enqueue` (synchronous, matching the diagram's `void`
  return) adds an `OutboxMessage` to the same `PaymentsDbContext` instance
  `EfPaymentRepository` uses, so `IPaymentRepository.SaveChangesAsync`
  flushes both together — this is the actual "transactional" part of
  "transactional outbox": the message and the state that produced it can
  never end up out of sync.
- **`PaymentWebhookHandler` has no controller route**: 06-payments.md's
  `PaymentsController` doesn't list one, and `FakePaymentProvider`
  resolves synchronously anyway (nothing to call the webhook back). It's
  implemented as a real class with real behavior, exercised directly by
  tests, ready to be wired to an actual HTTP endpoint once a real
  provider (Stripe) needs one.
