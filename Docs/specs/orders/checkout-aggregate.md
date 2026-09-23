# Orders aggregate and checkout flow

## What

Implement the Orders module per
[05-orders.md](../../diagrams/implementation-class/05-orders.md):

- Expand the existing `Order`/`OrderItem` aggregate: `OrderNumber`,
  shipping/billing `Address`, discount/shipping/tax amounts, customer/
  internal notes, ship/deliver timestamps, and the corresponding new
  behavior (`SetAddresses`, `SetInternalNotes`, `ApplyDiscount`,
  `SetShippingAmount`, `SetTaxAmount`, `OrderItem.DecreaseQuantity`/
  `ApplyDiscount`).
- `IOrderNumberGenerator`/`SequentialOrderNumberGenerator`, backed by a
  PostgreSQL sequence, producing human-readable order numbers
  (`ORD-2026-000123`) instead of exposing the internal `Guid` to
  customers.
- The full set of Application use cases this diagram adds:
  `SetOrderAddressesUseCase`, `ConfirmOrderUseCase`,
  `MarkOrderPaymentFailedUseCase`, `CancelOrderUseCase`,
  `GetOrderByIdUseCase`, `ListCustomerOrdersUseCase` (`CreateOrderHandler`
  already exists and gets updated for the new `Order.Create` signature).
- `IInventoryService`/`InventoryServiceAdapter`, wrapping Inventory's
  already-implemented `ReserveStockUseCase`/`ReleaseReservationUseCase`/
  `ConsumeReservationUseCase` behind the Application Contract indirection
  (section 7) — the same pattern `ProductCatalogAdapter` already
  established for Catalog.
- Domain event dispatch actually wired for Orders (`EfOrderRepository`
  calling `IDomainEventDispatcher.DispatchAsync` after a successful
  save — Orders only ever touches one aggregate root per operation, so
  no `IUnitOfWork` is needed here, unlike Inventory), plus
  `OrderStatusHistoryProjector` reacting to `OrderCreated`/`OrderConfirmed`/
  `OrderCancelled`/`OrderPaymentFailed` to populate
  `OrderStatusHistoryPersistenceModel` — the second real
  `IDomainEventHandler<T>` in the codebase, after `StockMovementRecorder`.
- `OrdersController`, Requests/Responses, `OrderPresenter`.

## Why

Order is "the main aggregate of the system" (section 9) and the reason
Inventory/Catalog exist as adapters in the first place — this feature is
what actually exercises those adapters for the first time from the
consuming side, closing the loop that `ProductCatalogAdapter` (built
while fixing Orders' persistence gap) started.

`OrderNumber` and the shipping/billing addresses are what turn `Order`
from an internal record into something that can actually back a checkout
flow and an order-confirmation screen — a customer is never shown a raw
`Guid`, and an order without an address can't ship.

## Scope update

Originally this spec deferred everything Payments-dependent
(`IPaymentGateway`/`PaymentGatewayAdapter`/`RequestOrderPaymentUseCase`,
and the two integration-event handlers) to a follow-up feature, since
Payments' own Application/Infrastructure didn't exist yet. Superseded:
the user asked to implement the Payments module (06-payments.md) now
instead of deferring, so this feature's scope grows to include it — see
`Docs/specs/payments/payment-processing.md` for that module's own spec.
Once it lands, this Orders feature also includes:

- `IPaymentGateway`/`PaymentGatewayAdapter` (calls Payments'
  `CreatePaymentUseCase`), `RequestOrderPaymentUseCase`, and
  `OrdersController.RequestPaymentAsync`.
- `PaymentAuthorizedIntegrationEventHandler`/`PaymentFailedIntegrationEventHandler`,
  now reachable because Payments' Transactional Outbox
  (`OutboxPublisherBackgroundService`) actually publishes
  `PaymentAuthorized`/`PaymentFailed` — see the Payments spec for how
  "publish" works before RabbitMQ exists.

## Out of scope (still deferred)

- A real Stripe `IPaymentProvider` implementation — `FakePaymentProvider`
  remains the only one, per section 14/15's own phasing.
- RabbitMQ / a real message broker (section 21) — the outbox publishes by
  calling the in-process `IDomainEventDispatcher` directly, not by
  putting anything on a queue; see the Payments spec.
