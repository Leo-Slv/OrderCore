# Implementation plan — unpaid orders stop holding stock (V5, part 2)

Implements `Docs/specs/orders/unpaid-order-expiry.md`. Small and
independent of part 1, so it can go first. Same workflow: a `v5/<topic>`
branch, green CI, fast-forward into `master`.

## Stage 1 — Orders without a payment expire after 30 minutes

- `Order.PaymentRequestedAt`, set by `RequestPayment`; migration
  `AddPaymentRequestedAt` fills it from `CreatedAt` for orders already
  `PendingPayment`.
- `IOrderRepository.ListPendingPaymentRequestedBeforeAsync(cutoff, limit)`
  (ids, oldest first, index on `(Status, PaymentRequestedAt)`).
- `ExpireUnpaidOrderUseCase(orderId)`: reloads the order; if it is still
  `PendingPayment` past the deadline **and** `IPaymentGateway` reports no
  payment for it, releases its reservations (`IInventoryService`) and
  fails it with reason `payment_not_started` (`Order.MarkPaymentFailed`,
  decision 1), so `OrderPaymentFailed` goes out through the outbox as for
  any failed payment (timeline, real-time screens, and V5's e-mail). An
  order that has a payment is left to the payment window. Audited and
  counted (`OrdersMetrics`: expired unpaid orders).
- `UnpaidOrderExpiryBackgroundService` every minute, each order in its own
  scope (`Orders:UnpaidOrderExpiry`: window 30 minutes, interval,
  batch size).
- **A late authorization for an order that no longer waits is voided.**
  If a checkout replay started the payment at the same moment the order
  expired, the authorization would arrive for a `PaymentFailed` order and
  `ConfirmOrderUseCase` would skip it, leaving the money held until the
  authorization expired. `ConfirmOrderUseCase` now settles the payment
  (`IPaymentGateway.SettleForCancellationAsync`, idempotent: void) when
  the order is `PaymentFailed` or `Cancelled`, and logs it.

Tests: unit (expired vs. not yet; with a payment → untouched; moved on →
untouched; a late authorization on a failed order is voided); integration
(a checkout whose payment request failed, clock/window shortened through
configuration: after the window the order is `PaymentFailed` with
`payment_not_started` and the stock is available again; repeating the
checkout within the window still starts the payment).

## Stage 2 — Reservations live at most 2 hours

- `InventoryReservation.Create` sets `ExpiresAt = now + lifetime`
  (`Inventory:Reservations:Lifetime`, 2 hours, decision 2); migration
  `BackfillReservationExpiry` sets `ReservedAt + 2 h` on active
  reservations without one.
- `IInventoryReservationRepository.ListExpiredActiveAsync(now, limit)`.
- `ReservationExpiryBackgroundService` every 5 minutes runs the existing
  `ExpireReservationUseCase` for each (own scope). Every expiry is logged
  as a **warning** — with the order and product ids — because the order
  deadlines should always get there first, and counted
  (`InventoryMetrics`, released with reason `expired`, already there).
- `ExpireReservationUseCase` tolerates a reservation that moved on
  (released or consumed meanwhile) instead of throwing.

Tests: unit (lifetime set on create; expire only active past the
deadline); integration (an active reservation past its lifetime is
expired and its units are available; a consumed one isn't touched).

## Stage 3 — Docs

- `05-orders.md` (field, use case, job, the late-authorization void),
  `04-inventory.md` (lifetime, job); `ORDERCORE_CONTEXT.md` (the order
  and payment deadlines together); `CLAUDE.md` if a convention emerges;
  `README.md` (`payment_not_started` as a failure reason the storefront
  shows); execution notes.

## Commits

1. `feat(orders): unpaid orders expire and release their stock`
2. `feat(inventory): reservations expire as a safety net`
3. `docs: ...`
