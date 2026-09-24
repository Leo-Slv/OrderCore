# Cross-module audit trail (WHAT/WHY)

## Problem

The AuditLogs module (`Modules/AuditLogs/`) already exists end to end —
`AuditLog`, `IAuditLogRepository`/`InMemoryAuditLogRepository`,
`AuditLogService`, `ListAuditLogsUseCase`, the read-only paginated
controller — and is registered in `Program.cs`. But 07-auditlogs.md's own
"Consumido por outros módulos" note documents the gap plainly: no other
module actually calls `IAuditLogService.RecordAsync`. `AuditLogActionNames`
already lists the 16 actions across Orders/Payments/Inventory/Catalog/
Customers, but they're unused constants. On top of that, the module itself
has zero automated tests, unlike every other module in the project.

## Why

An audit trail that never receives a single entry isn't a feature, it's
dead code with a plan attached. The module was built to answer "what
happened to this order/payment/stock item," but today it can't answer
anything. Closing this gap is what turns `AuditLogActionNames` from an
aspirational list into the module actually doing its job.

## Scope

1. Every use case whose outcome corresponds to one of the 16
   `AuditLogActionNames` constants calls `IAuditLogService.RecordAsync`
   after its state change is durably saved (`SaveChangesAsync` succeeded),
   the same "record after the side effect committed" ordering already used
   for domain-event dispatch (`EfOrderRepository.SaveChangesAsync`,
   `InventoryUnitOfWork.SaveChangesAsync`).
2. Unit tests for the AuditLogs module itself: `AuditLog` (Domain),
   `AuditLogService`, `ListAuditLogsUseCase`, `InMemoryAuditLogRepository`,
   `AuditLogPresenter`.
3. Existing unit tests for use cases that gain the new `IAuditLogService`
   constructor dependency are updated to inject a fake, following this
   project's per-module fake convention (`Tests/OrderCore.UnitTests/<Module>/Fake*.cs`).

## Out of scope

- Persisting `AuditLog` to Postgres (`InMemoryAuditLogRepository` stays —
  07-auditlogs.md's own diagram shows an in-memory repository, not EF
  Core persistence; changing that is a separate, unrequested decision).
- Any notion of "current user" / authentication. The project has no auth
  middleware yet, so every `RecordAsync` call passes `userId: null` — see
  Decisions below.
- Retrying or queuing a failed `RecordAsync` call. `InMemoryAuditLogRepository`
  cannot fail in a way that would need that, and no module in this project
  wraps a normal call in defensive try/catch for a failure mode that can't
  happen (see claude.md's guidance on not adding unneeded error handling).

## Decisions (not asked as open questions)

- **`userId` is always `null`.** There's no `ICurrentUserAccessor`/HTTP
  context concept anywhere in the codebase yet — every candidate call site
  is a plain use case with no authenticated-principal parameter. Inferable
  from the existing code, not a real open question.
- **Cross-module dependency shape**: other modules depend on
  `IAuditLogService` (`OrderCore.Api.Modules.AuditLogs.Application.Services`)
  directly, with no per-module adapter — 07-auditlogs.md's own
  "Consumido por outros módulos" note already specifies this explicitly,
  unlike Orders' `IProductCatalog`/`IInventoryService`/`IPaymentGateway`
  indirection. AuditLogs is cross-cutting/technical (like `Shared/`), not a
  business bounded context needing isolation for a future extraction, so
  the stricter "each module defines its own contract + adapter" pattern
  used for Orders↔Payments/Inventory/Catalog doesn't apply here.
- **`entityName`/`entityId` per action** — the aggregate whose state
  actually transitioned:
  - Orders (`OrderCreated`/`OrderConfirmed`/`OrderCancelled`/`OrderPaymentFailed`):
    `entityName = "Order"`, `entityId = order.Id`.
  - Payments (`PaymentAuthorized`/`PaymentFailed`/`PaymentCaptured`/`PaymentRefunded`):
    `entityName = "Payment"`, `entityId = payment.Id`.
  - Inventory (`InventoryReserved`/`InventoryReleased`/`InventoryConsumed`/`InventoryExpired`):
    `entityName = "InventoryReservation"`, `entityId = reservation.Id` — the
    reservation, not the `StockItem`, is what actually carries the
    Reserved/Released/Consumed/Expired lifecycle these four actions name.
  - Catalog (`ProductCreated`/`ProductPriceChanged`/`ProductPublished`):
    `entityName = "Product"`, `entityId = product.Id`.
  - Customers (`CustomerCreated`): `entityName = "Customer"`,
    `entityId = customer.Id`.
- **Metadata is minimal, not exhaustive** — just enough to identify the
  related aggregate/amount/reason without duplicating the full entity
  (e.g. `OrderCreated` carries `customerId`/`totalAmount`, not every order
  item). Consistent with `AuditLogOutput.Metadata` being a flat
  `IReadOnlyDictionary<string, string>`, not a nested payload.
- **`CreatePaymentUseCase`/`AuthorizePaymentUseCase` both call `RecordAsync`
  for `PaymentAuthorized`/`PaymentFailed`** — they duplicate the same
  authorize-or-fail branch (`CreatePaymentUseCase` synchronously authorizes
  on creation; `AuthorizePaymentUseCase` is the separate retry path — see
  Docs/specs/payments/payment-processing.md), so both branches need the
  same audit coverage independently.
- **`RequestRefundUseCase` only records `PaymentRefunded` when
  `Payment.Refund()` is actually called** (i.e. the refund completed the
  full payment amount, not a partial one) — mirrors the same condition
  that already gates the `PaymentRefunded` integration event in that use
  case.
