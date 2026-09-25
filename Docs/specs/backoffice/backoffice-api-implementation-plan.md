# Implementation plan — Backoffice API

Implements `Docs/specs/backoffice/backoffice-api.md`. No new module:
each backoffice screen is served by the module that owns its data, with
the cross-module reads going through the existing contract direction
(Orders → Customers/Inventory/Payments, Catalog → Inventory,
Identity → Customers). Staged like the previous features: each stage
leaves `dotnet build`, `dotnet test` (all three projects) and
`dotnet format OrderCore.sln --verify-no-changes` passing and ends in its
own commit(s).

## Decisions made while planning (not asked; conventional defaults)

- **Every new endpoint is `Admin`-policy.** No storefront behavior
  changes, except the two listed under Orders (idempotent payment
  consumers) and Identity (a deactivated customer can't sign in).
- **Route rule for admin views.** Commands stay on the resource they act
  on (`orders/{id}/ship`, `catalog/products/{id}/price`,
  `customers/{id}/deactivate`). A *read* that exists for customers too but
  needs a richer, admin-only shape gets its own route under `admin/`
  (`admin/orders`, `admin/orders/{id}`, `admin/catalog/products`,
  `admin/dashboard`), served by an `<Module>AdminController` in the owning
  module. One response shape per route, never a shape that changes with
  the caller's role — the OpenAPI document can't describe that. Modules
  that are admin-only already (Inventory, Payments, Customers' `{id}`
  routes, AuditLogs) keep their routes.
- **Every step of a cross-module operation is idempotent**, because there
  is no distributed transaction (one `DbContext` per module): capture on
  an already-captured payment, void on an already-voided one, returning
  stock already returned, are no-ops. A failure halfway leaves the order
  where it was, and repeating the request finishes the job. Same
  reasoning as checkout's compensation sequence.
- **Payment consumers tolerate orders that moved on.** Today a
  `PaymentAuthorized`/`PaymentFailed` event for an order that is no longer
  `PendingPayment` throws `invalid_order_state` inside the outbox
  publisher, which never marks the message processed and so blocks every
  later message. Cancelling a `PendingPayment` order as an admin makes
  that reachable, so `ConfirmOrderUseCase` and
  `MarkOrderPaymentFailedUseCase` skip (and log) an order that is no
  longer `PendingPayment`.
- **Cancelling while the payment is still in flight** (`Pending`/
  `Processing`, i.e. the provider hasn't answered) is refused with
  `409 payment_in_progress` — there is nothing to void yet. It can be
  retried seconds later.
- **Audit logging is best effort.** It is written after the business
  change commits, in its own transaction (like `StockMovementRecorder`).
  Today a failed audit write turns a request whose change already
  committed into a 500, which invites a retry of a non-idempotent
  operation. Once the log is persisted (Stage 1), `AuditLogService` logs
  the failure and returns instead of throwing.
- **A deactivated customer can't sign in or refresh.** Identity already
  depends on Customers (`ICustomerRegistry`), so sign-in and refresh ask
  whether the linked customer is active and answer
  `401 account_inactive` if not. An access token already issued lives
  out its 15 minutes; checkout already refuses an inactive customer.
- **Customer detail "with their orders" is composed by the frontend**:
  `GET customers/{id}` plus the existing `GET orders/customers/{customerId}`.
  Customers can't call Orders without a module cycle, and nothing is
  gained by adding one.
- **The dashboard lives in Orders** (`GetDashboardUseCase`): it is
  order-centric, and Orders already reaches Customers and Inventory
  through its contracts, so no new dependency direction is needed.
  Figures for a period `[from, to)`, default the last 30 days:
  - order count by status, for orders created in the period;
  - revenue: `TotalAmount` of `Confirmed`/`Processing`/`Shipped`/
    `Delivered` orders confirmed in the period;
  - new customers created in the period;
  - low-stock and out-of-stock product counts (now, not per period);
  - the 10 most recent orders.
- **Stock stays per product**, as it is today. `StockItem.ProductVariantId`
  remains unused; per-variant stock is out of scope.
- **The public product listing keeps its current behavior** (admins also
  see drafts there). The new `admin/catalog/products` listing is what the
  backoffice uses.
- **Customer self-cancel stays out.** The settlement it depended on now
  exists, but the spec doesn't ask for it; it's a small follow-up.

## Stage 1 — AuditLogs: persistence and filters

- `Infrastructure/Persistence`: `AuditLogsDbContext` (schema like the
  other modules), `AuditLogPersistenceModel`, `AuditLogConfiguration`
  with indexes on (`EntityName`, `EntityId`), `UserId` and `CreatedAt`,
  `AuditLogMapper`, `EfAuditLogRepository`, `AuditLogsDbContextFactory`,
  and migration `InitialAuditLogsSchema`. `AuditLog` gets an
  `internal static Rehydrate`. `InMemoryAuditLogRepository` is removed.
- `ListAuditLogsInput` gains `EntityName`, `EntityId`, `UserId` and
  `Action` filters. `IAuditLogRepository.ListPagedAsync` takes the
  filter (newest first). The unused `ListByEntityAsync`/`ListByUserAsync`
  are folded into it.
- `AuditLogService.RecordAsync` becomes best effort: it catches, logs
  (`ILogger`) and returns.
- `GET audit-logs?entityName=Order&entityId=…&userId=…&action=…&page=…`.

Tests:
- integration test for `EfAuditLogRepository` (filters, paging, order);
- unit test that a failing repository doesn't make `RecordAsync` throw;
- `ApiDatabase` migrates the new context.

## Stage 2 — Payments: void, capture by order, settlement, list

Domain:
- `PaymentStatus.Voided`.
- `Payment.Void(now)` from `Authorized` only, sets `VoidedAt`.
- `IPaymentProvider.VoidAsync` (`PaymentVoidResult`), implemented by
  `FakePaymentProvider`. In `Declined` mode, void still succeeds; a
  failure mode is added for tests.

Application:
- `CapturePaymentUseCase` becomes idempotent: `Captured` → returns as is.
  Add `CapturePaymentForOrderAsync(orderId)`, used by Orders.
- `VoidPaymentUseCase` (idempotent). A provider failure answers
  `409 payment_void_failed`.
- `SettlePaymentForCancellationUseCase(orderId)` returns `NothingToSettle` |
  `Voided` | `Refunded`:

  | Payment state | What happens |
  |---|---|
  | none, `Failed`, `Voided`, `Refunded` | nothing |
  | `Pending`, `Processing` | `409 payment_in_progress` |
  | `Authorized` | void |
  | `Captured` | full refund of the remaining balance, through the existing refund path (outbox `PaymentRefunded` included) |

- `ListPaymentsUseCase`: filter by status, method, created from/to;
  paged, newest first. `IPaymentRepository.ListAsync`.
- `GetPaymentByIdUseCase`.
- Audit action names `PaymentVoided`.

Persistence: `VoidedAt` column, migration `AddPaymentVoid`. Status is
already stored as text, so no data change is needed.

Presentation:
- `GET payments` (paged, with status/method/from/to).
- `GET payments/{id}`, with refunds.
- The refund endpoint already exists.

Tests:
- domain: void transitions;
- use cases: every settlement branch; capture/void idempotency;
- repository: list filters.

## Stage 3 — Inventory: stock records, receiving, reorder level, history

Domain:
- `StockItem.SetReorderLevel(int)` (≥ 0).
- `Receive` and `Adjust` raise `InventoryStockMovementRecorded`
  (`Inbound`/`Adjustment`), so manual changes reach the movement history
  (today only reservations do). `Create` with an initial quantity > 0
  raises an `Inbound` too.
- The event and `stock_movements` gain a nullable `Reason`, so an
  adjustment keeps its reason.
- `ReservationStatus.Returned`. `InventoryReservation.Return(now)` goes
  from `Consumed` only and raises an `Inbound` movement referencing the
  order.

Application:
- `EnsureStockItemUseCase(productId)`: creates the record with 0 units if
  it doesn't exist. Idempotent; a duplicate insert racing on the unique
  `ProductId` index is treated as "already exists".
- `ReceiveStockUseCase(productId, quantity)`.
- `SetReorderLevelUseCase(productId, level)`.
- `ReturnOrderStockUseCase(orderId)`: for every `Consumed` reservation of
  the order, `Return` it and `Receive` its quantity back, through
  `IUnitOfWork` (two aggregate roots). Idempotent, since `Returned`
  reservations are skipped.
- `ListStockItemsUseCase(state: All | Low | Out, productIds?, page)` and
  `GetStockLevelsUseCase(productIds)` return on-hand, reserved,
  available, reorder level and state.
- `GetStockSummaryUseCase` returns the low-stock and out-of-stock counts.
- `ListStockMovementsUseCase(productId, page)`, through a read-only
  `IStockMovementReader` (same shape as Orders' `IOrderStatusHistoryReader`).
- `ListReservationsUseCase(productId | orderId)`.

Persistence: migration `AddStockMovementReason`. `Returned` fits the
existing text column.

Presentation (`inventory/stock-items/{productId}/…`):
- `POST …/receive`;
- `PUT …/reorder-level`;
- `GET …/movements`;
- `GET …/reservations`;
- `GET inventory/stock-items?state=Low|Out` (figures by product id — the
  screen itself uses Catalog's admin listing, Stage 4).

Tests:
- domain: reorder level; `LowStock` becomes reachable; return
  transitions; movements raised;
- use cases: return is idempotent; ensure is idempotent;
- integration: `Returned` and `Reason` round-trip; movements recorded for
  receive/adjust.

## Stage 4 — Catalog: stock records, product operations, admin listing

Contract: Catalog's `IStockAvailabilityProvider` becomes `IStockLevels`
(same adapter, extended):
- `GetAvailabilityAsync(ids)` — unchanged, for the storefront;
- `EnsureStockRecordAsync(productId)`;
- `GetStockLevelsAsync(ids)`, returning Catalog's own `ProductStockLevel`;
- `ListProductIdsByStockStateAsync(state)`.

The adapter calls the Stage 3 use cases.

Stock record creation (decision 6 in the spec): `CreateProductUseCase`
and `PublishProductUseCase` call `EnsureStockRecordAsync` after saving the
product. Publish being idempotent on the stock side also repairs a
product whose creation failed between the two saves, and any product
created before this feature.

Domain:
- `Product.SetCompareAtPrice(decimal?)`: must be greater than the
  current price, `null` clears it. `ChangePrice` clears a compare-at
  price that is no longer greater.
- The remaining operations already exist: discontinue, add/remove/
  reorder images, add/remove variants.

Application: one use case per operation, following
`ChangeProductPriceUseCase` (which already exists, with no endpoint):
- `SetCompareAtPriceUseCase`;
- `DiscontinueProductUseCase`;
- `AddProductImageUseCase`, `RemoveProductImageUseCase`,
  `ReorderProductImagesUseCase`;
- `AddProductVariantUseCase`, `RemoveProductVariantUseCase`.

Audit covers price change (exists) and discontinue.

Admin listing:
- `ListAdminProductsUseCase`: filters by status (Draft/Active/
  Discontinued), category, search by name/SKU, and stock state (Low/Out).
- The stock-state filter first asks Inventory for the matching product
  ids, then pages the products among them.
- Each row carries its stock figures (`GetStockLevelsAsync` for the
  page).

Presentation:
- `CatalogAdminController`:
  `GET admin/catalog/products` → `AdminProductSummaryResponse` (SKU,
  status, price, compare-at price, on-hand/reserved/available, reorder
  level, stock state).
- `CatalogController`, Admin policy:
  - `PUT catalog/products/{id}/price`;
  - `PUT catalog/products/{id}/compare-at-price`;
  - `POST catalog/products/{id}/discontinue`;
  - `POST catalog/products/{id}/images`;
  - `DELETE catalog/products/{id}/images/{imageId}`;
  - `PUT catalog/products/{id}/images/order`;
  - `POST catalog/products/{id}/variants`;
  - `DELETE catalog/products/{id}/variants/{variantId}`.

Tests:
- domain: compare-at rules;
- use cases: the stock record is ensured on create/publish;
- integration: the admin listing's stock filter and paging; an image and
  a variant added to an already-saved product (the `ValueGeneratedNever`
  lesson).

## Stage 5 — Customers and Identity: list, deactivate, sign-in check

Customers:
- `ICustomerRepository.ListAsync(search, page)`: search by name or e-mail,
  case-insensitive, newest first.
- `ListByIdsAsync(ids)`.
- `CountCreatedBetweenAsync(from, to)`.
- Use cases `ListCustomersUseCase`, `DeactivateCustomerUseCase`,
  `ReactivateCustomerUseCase` (idempotent), `GetCustomersByIdsUseCase`,
  `CountNewCustomersUseCase`, `IsCustomerActiveUseCase`.
- Audit actions `CustomerDeactivated`/`CustomerReactivated`.
- Endpoints (Admin):
  - `GET customers?search=&page=`;
  - `POST customers/{id}/deactivate`;
  - `POST customers/{id}/reactivate`.

Identity:
- `ICustomerRegistry.IsActiveAsync(customerId)`.
- `SignInUseCase` and `RefreshSessionUseCase` refuse a customer account
  whose customer is inactive, with `401 account_inactive`. The account
  itself isn't touched: reactivating the customer restores access.

Tests:
- use cases;
- the search's case-insensitivity against Postgres;
- a deactivated customer can't sign in or refresh, and regains access
  when reactivated.

## Stage 6 — Orders: fulfilment, cancellation with settlement, admin views

Domain:
- New events `OrderProcessingStarted`, `OrderShipped`, `OrderDelivered`,
  raised by the existing transitions. `OrderStatusHistoryProjector`
  records them, which closes the gap in the timeline.
- `Order.EnsureCanBeCancelled()`: the same rule `Cancel` applies, checked
  before any side effect.

Contracts (Orders' own types):
- `IPaymentGateway`:
  - `CaptureForOrderAsync(orderId)`;
  - `SettleForCancellationAsync(orderId)`;
  - `GetPaymentSummariesAsync(orderIds)`, for the list;
  - `GetPaymentDetailsAsync(orderId)`, returning `OrderPaymentDetails`
    with provider, reference, failure reason and refunds.
- `IInventoryService`:
  - `ReturnConsumedStockAsync(orderId)`;
  - `GetReservationsAsync(orderId)`;
  - `GetStockSummaryAsync()`, for the dashboard.
- `ICustomerDirectory`:
  - `GetCustomersAsync(ids)`, returning `OrderCustomerSnapshot` (id,
    name, e-mail);
  - `CountNewCustomersAsync(from, to)`.

Use cases:
- `StartOrderProcessingUseCase`, `DeliverOrderUseCase`.
- `ShipOrderUseCase`:
  1. capture through the gateway; on failure,
     `409 payment_capture_failed` with the provider's reason, and the
     order stays `Processing`;
  2. then `Ship` and save.
- `CancelOrderUseCase` rewritten, in this order:
  1. `EnsureCanBeCancelled`;
  2. settle the payment;
  3. release reservations still held;
  4. return consumed stock;
  5. `Cancel` and save;
  6. audit, with the settlement outcome in the metadata.

  Every step before the save is idempotent, so a failure leaves the order
  unchanged and a retry completes it.
- `ConfirmOrderUseCase`/`MarkOrderPaymentFailedUseCase` skip orders that
  are no longer `PendingPayment` (see decisions).
- `SetOrderInternalNotesUseCase`.
- `ListOrdersUseCase`: filters status, customer, created from/to; newest
  first; paged. Each row gets the customer's name/e-mail and the payment
  status, fetched in one batch per page. `IOrderRepository.ListAsync`.
- `GetAdminOrderDetailsUseCase`: returns the customer view (existing
  `OrderDetailsOutput`) plus internal notes, the customer, reservations
  and the payment details.
- `GetDashboardUseCase` (figures above); `IOrderRepository` gains the two
  aggregate queries it needs (count by status, revenue).
- Audit actions `OrderProcessingStarted`, `OrderShipped`,
  `OrderDelivered`.

Presentation:
- `OrdersAdminController`:
  - `GET admin/orders`;
  - `GET admin/orders/{id}`;
  - `GET admin/dashboard?from=&to=`.
- `OrdersController`, Admin policy:
  - `POST orders/{id}/start-processing`;
  - `POST orders/{id}/ship`;
  - `POST orders/{id}/deliver`;
  - `PUT orders/{id}/internal-notes`;
  - the existing `POST orders/{id}/cancel`, now settling.

Tests:
- domain: the new events;
- use cases:
  - ship when capture fails;
  - cancel for every payment state (none, authorized → void, captured →
    refund, in flight → 409) and every stock state (reserved → release,
    consumed → return);
  - a retried cancel after a failure in the middle;
  - a late `PaymentAuthorized` for a cancelled order is ignored;
- repository: list filters, dashboard aggregates.

## Stage 7 — HTTP-level tests

Using `ApiDatabase`:
- **Fulfilment:** the admin creates a product (stock record appears),
  receives stock, sets the reorder level, and publishes. A customer
  checks out; the order is confirmed by the outbox. The admin then starts
  processing and ships (payment `Captured`) and delivers. Afterwards:
  - the status history shows every step;
  - the audit timeline for the order lists them with the admin as actor;
  - the storefront shows `LowStock` once available ≤ reorder level.
- **Cancel a confirmed order:** the payment is `Voided`, stock comes back
  (movements show the return), and the order is `Cancelled`.
- **Capture declined:** with the provider in a declining mode, ship
  answers 409 and the order stays `Processing`.
- **Customers:** deactivate, then sign-in fails; reactivate, then it
  works.
- **Access:** `EndpointAccessTests` covers the new admin routes (customer
  token → 403, anonymous → 401).

## Stage 8 — Docs

- Diagrams:
  - `05-orders`: new use cases, contracts and events;
  - `06-payments`: void, `Voided`, settlement;
  - `04-inventory`: reorder level, return, movement reason, new use
    cases;
  - `03-catalog`: `IStockLevels`, the admin listing, product operations;
  - `02-customers`: list/deactivate;
  - `07-auditlogs`: now persisted — drop the in-memory note;
  - `08-identity`: the active-customer check;
  - overview: the edges the new contracts add (all in existing
    directions).
- `ORDERCORE_CONTEXT.md`:
  - contracts table (section 7);
  - the order state machine now fully reachable;
  - capture on ship and settlement on cancel;
  - the audit log persisted;
  - the new migrations.
- `CLAUDE.md`:
  - the `admin/` route rule;
  - idempotent steps in cross-module operations;
  - integration-event consumers must tolerate state that moved on;
  - AuditLogs has persistence;
  - audit is best effort.
- `README.md`: backoffice endpoint table; the order lifecycle as it now
  runs end to end.
- Execution notes appended to this plan.

## Commits

One or more per stage, e.g.:

1. `feat(auditlogs): persist the audit log and filter it by entity and actor`
2. `feat(payments): void authorizations, capture by order and settlement on cancel`
3. `feat(inventory): stock records, receiving, reorder level and movement history`
4. `feat(catalog): product operations, admin listing with stock and automatic stock records`
5. `feat(customers,identity): customer list and deactivation that blocks sign-in`
6. `feat(orders): fulfilment, cancellation with settlement and admin order views`
7. `test: backoffice flows over HTTP`
8. `docs: ...` (diagrams separately from the rest)

The spec's new decisions (5–7) and this plan go first as
`docs(backoffice): ...`.
