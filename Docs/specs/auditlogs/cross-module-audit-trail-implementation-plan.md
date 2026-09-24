# Cross-module audit trail — implementation plan (HOW)

See `cross-module-audit-trail.md` for WHAT/WHY and the resolved decisions
(userId always null, entityName/entityId per action, minimal metadata).

## Stage 1 — AuditLogs module tests

The module has zero tests today. Add, under `Tests/OrderCore.UnitTests/AuditLogs/`:

- `AuditLogTests.cs` — `Create` sets all fields from its parameters.
- `FakeAuditLogRepository.cs` — same per-module fake convention as every
  other module (in-memory list, mirrors `InMemoryAuditLogRepository`'s
  shape closely enough to exercise `AuditLogService`/`ListAuditLogsUseCase`
  without depending on the real Infrastructure class).
- `AuditLogServiceTests.cs` — `RecordAsync` builds an `AuditLog` via
  `AuditLog.Create` and persists it through the repository; metadata
  dictionary round-trips.
- `ListAuditLogsUseCaseTests.cs` — pagination (`Page`/`PageSize`) returns
  the right slice and total count via `PagedResult<AuditLogOutput>`.
- `AuditLogPresenterTests.cs` — `ToResponse`/`ToInput` map fields 1:1.

## Stage 2 — Wire `IAuditLogService` into the 16 action call sites

Inject `IAuditLogService` (constructor parameter, same DI pattern as every
other dependency) and call `RecordAsync` right after the use case's own
`SaveChangesAsync` succeeds, passing `cancellationToken` through. No new
abstraction — direct dependency per the spec's resolved decision.

| Module | Use case | Action constant | entityId |
|---|---|---|---|
| Orders | `CreateOrderHandler` | `OrderCreated` | `order.Id` |
| Orders | `ConfirmOrderUseCase` | `OrderConfirmed` | `orderId` |
| Orders | `CancelOrderUseCase` | `OrderCancelled` | `command.OrderId` |
| Orders | `MarkOrderPaymentFailedUseCase` | `OrderPaymentFailed` | `orderId` |
| Payments | `CreatePaymentUseCase` | `PaymentAuthorized` or `PaymentFailed` (branch) | `payment.Id` |
| Payments | `AuthorizePaymentUseCase` | `PaymentAuthorized` or `PaymentFailed` (branch) | `payment.Id` |
| Payments | `CapturePaymentUseCase` | `PaymentCaptured` | `payment.Id` |
| Payments | `FailPaymentUseCase` | `PaymentFailed` | `payment.Id` |
| Payments | `RequestRefundUseCase` | `PaymentRefunded` (only when `Payment.Refund()` called) | `payment.Id` |
| Inventory | `ReserveStockUseCase` | `InventoryReserved` (only on success) | `reservation.Id` |
| Inventory | `ReleaseReservationUseCase` | `InventoryReleased` | `reservationId` |
| Inventory | `ConsumeReservationUseCase` | `InventoryConsumed` | `reservationId` |
| Inventory | `ExpireReservationUseCase` | `InventoryExpired` | `reservationId` |
| Catalog | `CreateProductUseCase` | `ProductCreated` | `product.Id` |
| Catalog | `ChangeProductPriceUseCase` | `ProductPriceChanged` | `productId` |
| Catalog | `PublishProductUseCase` | `ProductPublished` | `productId` |
| Customers | `RegisterCustomerUseCase` | `CustomerCreated` | `customer.Id` |

## Stage 3 — Update call sites and their DI registrations

- `Modules/<Module>DependencyInjection.cs` for Orders, Payments, Inventory,
  Catalog, Customers: no new registration needed for `IAuditLogService`
  itself (already registered by `AddAuditLogsModule()` in `Program.cs`),
  but the modified use cases still resolve it through the existing
  `services.AddScoped<UseCase>()` lines — nothing to change there since
  constructor injection is automatic.
- Verify `Program.cs` calls `AddAuditLogsModule()` before the host is
  built (already does — order relative to other `Add<Module>Module()`
  calls doesn't matter for DI resolution).

## Stage 4 — Update existing unit tests broken by the new constructor parameter

Each of these needs a `FakeAuditLogService` added to its own module's test
folder (per-module fake convention, no shared cross-module test double) and
threaded into every direct `new UseCase(...)` call:

- `Tests/OrderCore.UnitTests/Catalog/FakeAuditLogService.cs` →
  `CreateProductUseCaseTests.cs`, `PublishProductUseCaseTests.cs`.
- `Tests/OrderCore.UnitTests/Customers/FakeAuditLogService.cs` →
  `RegisterCustomerUseCaseTests.cs`.
- `Tests/OrderCore.UnitTests/Inventory/FakeAuditLogService.cs` →
  `ReserveStockUseCaseTests.cs`, `ExpireReservationUseCaseTests.cs`.
- `Tests/OrderCore.UnitTests/Payments/FakeAuditLogService.cs` →
  `CreatePaymentUseCaseTests.cs`, `RequestRefundUseCaseTests.cs`.

`ChangeProductPriceUseCase`, `ConfirmOrderUseCase`, `CancelOrderUseCase`,
`MarkOrderPaymentFailedUseCase`, `ReleaseReservationUseCase`,
`ConsumeReservationUseCase`, `AuthorizePaymentUseCase`,
`CapturePaymentUseCase`, `FailPaymentUseCase`, `CreateOrderHandler` have no
existing unit tests to update — left as-is (adding tests for use cases that
had none before is a separate, unrequested scope expansion).

## Stage 5 — Tests, format, docs, commit

- `dotnet build`, `dotnet test` (all three projects),
  `dotnet format OrderCore.sln --verify-no-changes`.
- Update `07-auditlogs.md`'s "Consumido por outros módulos" section to
  reflect the 16 real call sites (replacing "nenhum outro módulo chama
  ainda"). Update `README.md` if warranted.
- Commit in stages: (1) AuditLogs module tests, (2) the 16 call-site
  changes + their test fakes, grouped by source module, (3) docs.
