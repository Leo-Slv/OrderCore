# Implementation plan — Payment processing and outbox

Builds on `Docs/specs/orders/checkout-aggregate.md` (Order aggregate
expansion + Inventory/Catalog adapters, implemented first since Payments'
adapter needs `Order` to already have the shape it will report on).
Staged the same way every prior module was: Domain → Application →
Infrastructure → Presentation → Docs, `dotnet build`/`dotnet test`/
`dotnet format --verify-no-changes` passing before each commit.

## Stage 1 — Orders: Domain

`Order`/`OrderItem` expansion per 05-orders.md: `OrderNumber`, addresses,
discount/shipping/tax, notes, ship/deliver timestamps;
`OrderItem.DecreaseQuantity`/`ApplyDiscount`. Existing `OrderTests.cs`
gets updated for the new `Order.Create` signature.

## Stage 2 — Orders: Application + Infrastructure (Inventory/Catalog side only)

`IOrderNumberGenerator`/`SequentialOrderNumberGenerator` (Postgres
sequence), `IInventoryService`/`InventoryServiceAdapter`,
`SetOrderAddressesUseCase`/`ConfirmOrderUseCase`/
`MarkOrderPaymentFailedUseCase`/`CancelOrderUseCase`/`GetOrderByIdUseCase`/
`ListCustomerOrdersUseCase`, `EfOrderRepository` dispatching domain events
after save (no `IUnitOfWork` needed — one aggregate root per operation),
`OrderStatusHistoryProjector` (`IDomainEventHandler<T>` for all four Order
domain events) + its persistence model. Not yet: anything Payments-shaped
(`RequestOrderPaymentUseCase`, `IPaymentGateway`).

## Stage 3 — Orders: Presentation

`OrdersController` (Create/SetAddresses/GetById/ListByCustomer/Cancel —
`RequestPaymentAsync` added in Stage 8 once `IPaymentGateway` exists),
Requests/Responses, `OrderPresenter`.

## Stage 4 — Payments: Domain

`Payment` expansion (`Provider`, `CustomerPaymentMethodId`, timestamps),
`Refund` entity with the refundable-balance invariant living in
`Payment.RequestRefund`. `IPaymentProvider`/`FakePaymentProvider` already
exist and stay as-is.

## Stage 5 — Payments: Application

`IPaymentRepository`, `IOutboxWriter` (Application contract — the
concrete `OutboxWriter` is Infrastructure), DTOs, the six use cases. Also
promotes `IntegrationEvent` (existing placeholder types) to implement
`IDomainEvent` — see the spec's "How publish works" section.

## Stage 6 — Payments: Infrastructure

`PaymentsDbContext`, persistence models (expanded past the diagram's
abbreviated shape the same way every prior module's was), `PaymentMapper`,
`EfPaymentRepository` (dispatches domain events after save — Payment
itself doesn't raise any today, but the plumbing matches every other
module's for when it does), `OutboxMessage`/`OutboxWriter`,
`OutboxPublisherBackgroundService` (a real `BackgroundService`, polling
for unprocessed messages and dispatching them),
`PaymentWebhookHandler`. Migration `InitialPaymentsSchema`. Integration
tests (Testcontainers.PostgreSql) covering a full create→authorize→outbox→
publish round trip.

## Stage 7 — Payments: Presentation

`PaymentsController`, Requests/Responses, `PaymentPresenter`.

## Stage 8 — Orders: finish the checkout flow

`IPaymentGateway`/`PaymentGatewayAdapter` (calls Payments'
`CreatePaymentUseCase`), `RequestOrderPaymentUseCase`,
`OrdersController.RequestPaymentAsync`,
`PaymentAuthorizedIntegrationEventHandler`/`PaymentFailedIntegrationEventHandler`
(`IDomainEventHandler<PaymentAuthorized>`/`IDomainEventHandler<PaymentFailed>`,
calling `ConfirmOrderUseCase`/`MarkOrderPaymentFailedUseCase`). An
integration test exercising the whole loop: create order → request
payment → outbox publishes → Orders confirms.

## Stage 9 — Docs

Mark 05-orders.md and 06-payments.md as implemented (like every prior
diagram), documenting every deviation. Update `ORDERCORE_CONTEXT.md`
(Banco de dados section: Orders/Payments schemas; a note on the
outbox-as-dispatcher bridge) and `claude.md` (Persistence section: fifth
and sixth implemented modules; a new note on integration events
implementing `IDomainEvent` until RabbitMQ exists). Update `README.md`.
