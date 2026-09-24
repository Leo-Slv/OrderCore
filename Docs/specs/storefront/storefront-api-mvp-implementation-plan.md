# Implementation plan — Storefront API MVP

Implements `Docs/specs/storefront/storefront-api-mvp.md`. No new module:
every change lands in the module that already owns the concept, plus
one new cross-cutting piece under `Shared/` (error contract). Staged the
same way prior features were: each stage leaves `dotnet build`,
`dotnet test` (all three projects) and
`dotnet format OrderCore.sln --verify-no-changes` passing, and ends in
its own commit.

Stage order is driven by dependencies: Shared error contract first
(every later stage throws the new exception types), then the modules
Orders' checkout depends on (Inventory → Catalog → Customers →
Payments), then Orders itself, then Docs.

## Stage 1 — Shared: error contract, CORS, enum serialization

**Exception types** (none exist today — all 58 business failures are
`InvalidOperationException`, surfacing as HTTP 500):

- `Shared/Domain/Exceptions/DomainRuleViolationException` — carries a
  stable `Code` (snake_case string) plus message. Lives in
  `Shared/Domain` so module Domain code can throw it without referencing
  anything outside the domain kernel.
- `Shared/Application/Exceptions/NotFoundException` (`Code`, e.g.
  `order_not_found`) and `ConflictException` (`Code`, e.g.
  `insufficient_stock`, `price_changed`).

**Handler:** `Shared/Presentation/ExceptionHandling/ApiExceptionHandler`
(`IExceptionHandler`), registered with `AddProblemDetails()` +
`AddExceptionHandler<ApiExceptionHandler>()` and `app.UseExceptionHandler()`
in `Program.cs`. Mapping, all as RFC 7807 `ProblemDetails` with a `code`
extension:

| Exception | Status | `code` |
|---|---|---|
| `ArgumentException` (incl. `ArgumentOutOfRangeException`, thrown by every `Create`/value-object factory today) | 400 | `validation_error` |
| `DomainRuleViolationException` | 400 | its own `Code` |
| `NotFoundException` | 404 | its own `Code` |
| `ConflictException` | 409 | its own `Code` |
| `StockConcurrencyConflictException` (after `ReserveStockUseCase`'s retries) | 409 | `concurrency_conflict` |
| anything else | 500 | `internal_error` — no exception detail in the body outside Development |

The `ArgumentException` mapping means the 56 existing factory
`ArgumentException`s need no change. The existing
`InvalidOperationException`s are converted:

- the 24 "`X` was not found" throws (use cases) → `NotFoundException`;
- state-machine/invariant throws in Domain (`Order.EnsureStatus`,
  `Order.AddItem` past `Created`, `StockItem`, `Payment`, `Refund`,
  `Product.Publish`, etc.) → `DomainRuleViolationException` with a code
  per rule (`invalid_order_state`, `invalid_payment_state`,
  `refund_exceeds_balance`, …);
- anything left as `InvalidOperationException` afterwards is a genuine
  bug and correctly remains a 500.

Every controller's `[ProducesResponseType]` for 400/404/409 becomes
`typeof(ProblemDetails)` so Scalar shows the error shape.

**CORS:** `Shared/Presentation/Cors/CorsExtensions` (`AddStorefrontCors`/
`UseStorefrontCors`), a named policy reading `Cors:AllowedOrigins` from
configuration. `appsettings.json` gets
`"Cors": { "AllowedOrigins": ["http://localhost:3000"] }` (a dev origin,
not a secret). An empty/missing list allows no cross-origin requests.

**Enum serialization:** `AddControllers().AddJsonOptions(o =>
o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`,
so request enums introduced below (`PaymentMethod`, `ProductSortOrder`)
are accepted and documented as names, not integers. Existing responses
already emit `.ToString()` strings and are unaffected.

**Tests:** unit tests for `ApiExceptionHandler`'s mapping; an
integration test (the `WebApplicationFactory` host `HealthCheckTests`
already uses) asserting a 404 `ProblemDetails` body for
`GET api/orders/{unknown id}`, and the CORS preflight response for the
configured origin.

## Stage 2 — Inventory: availability query

- `IStockItemRepository.ListByProductIdsAsync(IReadOnlyCollection<Guid>)`
  (one query, no N+1 from listings), implemented in
  `EfStockItemRepository`.
- `StockItem.IsLowStock` (domain rule: `0 < QuantityAvailable <=
  ReorderLevel`), next to the existing computed `QuantityAvailable`.
- `GetStockAvailabilityUseCase` → `IReadOnlyList<StockAvailabilityOutput>`
  (`ProductId`, `QuantityAvailable`, `IsLowStock`). A product with no
  `StockItem` is reported as quantity 0. This use case is the *only*
  thing other modules' adapters call, so neither Catalog nor Orders
  touches `StockItem` itself.

Tests: unit tests for `IsLowStock`; repository integration test for
`ListByProductIdsAsync`.

## Stage 3 — Catalog: storefront listing and product detail

**Contract to Inventory** (new cross-module contract, same indirection as
Orders' `IProductCatalog`):

- `Catalog/Application/Contracts/IStockAvailabilityProvider`
  → `IReadOnlyDictionary<Guid, StockAvailability>`.
- `Catalog/Application/DTOs/StockAvailability` enum: `InStock`,
  `LowStock`, `OutOfStock` (resolved decision 2 — no quantities leave
  Catalog).
- `Catalog/Infrastructure/Adapters/InventoryStockAvailabilityAdapter`
  wraps `GetStockAvailabilityUseCase`.

**Listing:**

- `ListProductsFilter` gains `OnSale` (`CompareAtPrice > CurrentPrice`)
  and `Sort` (`ProductSortOrder`: `Name` (default, today's order),
  `PriceAsc`, `PriceDesc`, `Newest` (by `PublishedAt`)).
- `IProductRepository.ListAsync` returns `PagedResult<Product>` (count +
  page in the same filtered query).
- `ListProductsUseCase` returns `PagedResult<ProductSummaryOutput>`,
  fetching availability for the page's ids in one call.
- `GET catalog/products` returns `PagedResponse<ProductSummaryResponse>`
  in place (resolved decision 4). `ProductSummaryResponse`: `Id`, `Sku`,
  `Slug`, `Name`, `ShortDescription`, `Brand`, `CategoryId`,
  `CurrentPrice`, `CompareAtPrice`, `Currency`, `Status`,
  `PrimaryImageUrl`, `Availability`.

**Detail:**

- `IProductRepository.GetBySlugAsync(Slug)`.
- `GetProductBySlugUseCase`: only a product with `Status == Active` and
  `Active == true` is returned; otherwise `NotFoundException`
  (`product_not_found`) — a draft/discontinued product is
  indistinguishable from a missing one to the storefront.
- `ProductDetailsOutput`/`ProductResponse` (the existing response,
  enriched in place and still used by create/update/get-by-id): adds
  `Slug`, `ShortDescription`, `Description`, `Brand`, `CategoryId`,
  `Currency`, `Images` (`Url`, `AltText`, `IsPrimary`, `DisplayOrder`,
  ordered), `Variants` (`Id`, `Sku`, `Name`, `Attributes` parsed from
  `AttributesJson` into a string map by the presenter,
  `AdditionalPrice`), `Availability`. `ImageUrls` is removed (superseded
  by `Images`; no consumer yet).
- `GET catalog/products/by-slug/{slug}`.

Tests: unit tests for the use cases (fake `IStockAvailabilityProvider`);
repository integration tests for sort/`OnSale`/count and
`GetBySlugAsync`.

## Stage 4 — Customers: addresses usable at checkout

- `CustomerAddressResponse` enriched in place: `RecipientName`, `Phone`,
  `Street`, `Number`, `Complement`, `Neighborhood`, `City`, `State`,
  `PostalCode`, `Country`, `IsDefaultShipping`, `IsDefaultBilling`.
- `GetCustomerAddressUseCase(customerId, addressId)` returning the
  `CustomerAddress` or `NotFoundException` (`address_not_found`), and
  rejecting an inactive customer with `DomainRuleViolationException`
  (`customer_inactive`). This is what Orders' adapter calls in Stage 6.

Tests: unit tests for the new use case; presenter test for the full
address shape.

## Stage 5 — Payments: payment method

- `Payments/Domain/Enums/PaymentMethod`: `Card`, `Pix`.
- `Payment.Method`, set by `Payment.Create` (new required parameter);
  `PaymentPersistenceModel.Method` (string column), `PaymentMapper`,
  migration `AddPaymentMethod` defaulting existing rows to `Card`.
- `CreatePaymentCommand`/`CreatePaymentRequest` gain `Method`;
  `PaymentResponse` gains `Method`, `Currency`, `FailureReason`,
  `CreatedAt`, `AuthorizedAt` (needed by Orders' payment summary below
  and cheap to expose now).
- `FakePaymentProvider` is unchanged — both methods go through the same
  simulated authorization (resolved decision 1).

Tests: update `Payment`/`CreatePaymentUseCase` unit tests;
`EfPaymentRepositoryTests` round-trips `Method`.

## Stage 6 — Orders: Domain + Application

**Domain:**

- `Order.CheckoutIdempotencyKey` (nullable), set through
  `Order.Create`'s new optional parameter; unique per customer
  (`(CustomerId, CheckoutIdempotencyKey)` index, Stage 7).
- `Order.RequestPayment` raises a new `OrderPaymentRequested` domain
  event, and `OrderStatusHistoryProjector` handles it (`Created →
  PendingPayment`) — today that transition is never recorded, so the
  tracking timeline would skip it.
- State-machine throws converted to `DomainRuleViolationException`
  (Stage 1's list).

**Contracts (Orders/Application/Contracts):**

- `IProductCatalog`: `GetAsync` and a new `GetManyAsync` return an
  Orders-owned `CatalogProductSnapshot` (`Id`, `Sku`, `Slug`, `Name`,
  `PrimaryImageUrl`, `CurrentPrice`, `Currency`, `IsPurchasable`)
  instead of Catalog's `Product` domain entity — fixes an existing
  boundary leak (Orders' Application layer currently depends on
  `Catalog.Domain.Entities.Product`), since this contract is being
  changed anyway. `CreateOrderHandler` updated accordingly.
- `IInventoryService.GetAvailableQuantitiesAsync(productIds)` (wraps
  `GetStockAvailabilityUseCase`; used only to decide "enough / not
  enough", never returned to the client).
- `ICustomerDirectory.GetAddressAsync(customerId, addressId)` →
  `Shared.Domain.ValueObjects.Address` (wraps Stage 4's use case).
- `IPaymentGateway.RequestPaymentAsync` gains `PaymentMethodChoice`
  (Orders-owned enum in `Application/DTOs`, mapped to Payments'
  `PaymentMethod` by the adapter); new
  `GetPaymentSummaryAsync(orderId)` → `OrderPaymentSummary?`
  (`Status`, `Method`, `FailureReason`).
- `IOrderRepository.ListByCustomerIdAsync` becomes paged
  (`PagedResult<Order>`, newest first); new
  `FindByCheckoutIdempotencyKeyAsync(customerId, key)`.
- `IOrderStatusHistoryReader.ListAsync(orderId)` (read-side contract over
  `order_status_history`).

**Use cases:**

- `QuoteCartUseCase(lines)` — per line: product snapshot, current unit
  price, quantity, line total and at most one issue: `NotFound`,
  `Unavailable` (not purchasable), `InsufficientStock`, `PriceChanged`
  (only when the client sent an `ExpectedUnitPrice` that differs, and
  reporting the previous price). Returns the currency, total over valid
  lines, and `IsValid`. Mixed currencies → `DomainRuleViolationException`
  (`mixed_currencies`). Read-only: never reserves anything.
- `CheckoutUseCase(command)` — the single-step checkout:
  1. **Idempotency:** if `FindByCheckoutIdempotencyKeyAsync` finds an
     order, return it. If it is `PendingPayment` with no payment yet
     (the previous attempt died between saving the order and starting
     payment), request payment again — safe, because Payments already
     enforces one payment per order.
  2. Resolve shipping and billing addresses through `ICustomerDirectory`.
  3. Load products (`GetManyAsync`); reject non-purchasable products
     (`product_unavailable`) and mixed currencies. If the client sent an
     `ExpectedTotal` that differs from the recomputed total → `ConflictException`
     (`price_changed`).
  4. Build the order in memory (`Order.Create` with the idempotency key,
     `AddItem` per line, `SetAddresses`).
  5. Reserve stock (`IInventoryService.TryReserveOrderItemsAsync`, which
     already compensates partial reservations); failure →
     `ConflictException` (`insufficient_stock`). Nothing has been
     persisted yet at this point.
  6. `order.RequestPayment(now)` and save the order once (it goes
     straight from new to `PendingPayment`). If the save fails, release
     the reservations before rethrowing.
  7. `IPaymentGateway.RequestPaymentAsync` with the chosen method. The
     order is returned as `PendingPayment`; confirmation or failure still
     arrives asynchronously through the outbox, exactly as today.

  Orders, Inventory and Payments use separate `DbContext`s, so this is
  a sequence with compensation, not one database transaction — the same
  guarantee level `RequestOrderPaymentUseCase` already has, now with
  idempotent resumption instead of relying on the client to chain calls.
- `GetOrderDetailsUseCase` → `OrderDetailsOutput` (the `Order` plus
  `OrderPaymentSummary?`); `NotFoundException` (`order_not_found`).
- `ListCustomerOrdersUseCase` → `PagedResult<OrderSummaryOutput>`.
- `GetOrderStatusHistoryUseCase`.
- `RequestOrderPaymentUseCase` (kept, resolved decision 3) takes a
  `PaymentMethodChoice` too.

Tests: unit tests for `QuoteCartUseCase` (each issue type) and
`CheckoutUseCase` (happy path; idempotent replay; resume after a missing
payment; insufficient stock leaves nothing persisted; price changed;
unavailable product; unknown address), and for the new domain event.

## Stage 7 — Orders: Infrastructure + Presentation

**Infrastructure:**

- `OrderPersistenceModel.CheckoutIdempotencyKey` + unique filtered index
  on `(customer_id, checkout_idempotency_key)`; `OrderMapper`
  (`ToPersistence`/`ToDomain`/`Rehydrate`; the key never changes, so
  `ApplyChanges` doesn't touch it, and still copies `Version`); migration
  `AddCheckoutIdempotencyKey`.
- `EfOrderRepository`: paged list, lookup by idempotency key.
- `EfOrderStatusHistoryReader`.
- Adapters: `ProductCatalogAdapter` (snapshot mapping + `GetManyAsync`),
  `InventoryServiceAdapter` (new query), `CustomerDirectoryAdapter` (new),
  `PaymentGatewayAdapter` (method + summary). All registered in
  `OrdersDependencyInjection`.

**Presentation (`OrdersController`):**

| Route | Response |
|---|---|
| `POST orders/checkout` (header `Idempotency-Key`, required) | 202 + `Location` → `OrderResponse`; 400/404/409 `ProblemDetails` |
| `POST orders/cart/quote` | 200 `CartQuoteResponse` |
| `GET orders/{id}` | 200 `OrderResponse` (enriched in place) |
| `GET orders/{id}/status-history` | 200 `IReadOnlyList<OrderStatusHistoryEntryResponse>` |
| `GET orders/customers/{customerId}?page&pageSize` | 200 `PagedResponse<OrderSummaryResponse>` (changed in place, same reasoning as decision 4) |
| `POST orders/{id}/request-payment` | 202, now with a `RequestOrderPaymentRequest { PaymentMethod }` body |

`OrderResponse` gains `CreatedAt`, `ConfirmedAt`, `CancelledAt`,
`ShippedAt`, `DeliveredAt`, `SubtotalAmount`, `DiscountAmount`,
`ShippingAmount`, `TaxAmount`, `ShippingAddress`, `BillingAddress`,
`CustomerNotes`, `Payment` (`Status`, `Method`, `FailureReason`, or
null); `OrderItemResponse` gains `ProductSku`, `ProductImageUrl`.
`InternalNotes` is deliberately not exposed on the customer-facing
response.

Request/response names: `CheckoutRequest` (`CustomerId`, `Items`,
`ShippingAddressId`, `BillingAddressId`, `PaymentMethod`,
`CustomerNotes?`, `ExpectedTotal?`), `QuoteCartRequest`/
`QuoteCartLineRequest` (`ProductId`, `Quantity`, `ExpectedUnitPrice?`),
`CartQuoteResponse`/`CartQuoteLineResponse`, `OrderSummaryResponse`
(`Id`, `OrderNumber`, `Status`, `CreatedAt`, `TotalAmount`, `Currency`,
`ItemCount`), `OrderStatusHistoryEntryResponse` (`FromStatus`,
`ToStatus`, `Reason`, `ChangedAt`).

**Tests:** extend `CheckoutFlowTests` (Testcontainers) with the whole
storefront path: quote → checkout → outbox publishes → `GET orders/{id}`
shows `Confirmed` with payment `Authorized`/`Pix` → status history shows
`Created → PendingPayment → Confirmed`; replaying the checkout with the
same key returns the same order; a declined payment (`FakePaymentProvider`
`Declined`) ends in `PaymentFailed` with the failure reason visible.
`OpenApiTests` keeps passing. `ModuleBoundaryTests` gains a rule that
Orders' Application layer does not depend on any other module's Domain
namespace (now true after the `IProductCatalog` fix).

## Stage 8 — Docs

- Diagrams, with every new class/method/field/dependency above added
  back: `01-shared-kernel.md` (exception types, `ApiExceptionHandler`),
  `02-customers.md`, `03-catalog.md` (`IStockAvailabilityProvider` +
  adapter, new use case, sort/filter), `04-inventory.md`,
  `05-orders.md` (`CheckoutUseCase`, `QuoteCartUseCase`,
  `ICustomerDirectory`, `IOrderStatusHistoryReader`,
  `OrderPaymentRequested`, snapshot DTO), `06-payments.md`
  (`PaymentMethod`); `00-overview.md` gains the two new cross-module
  contracts (Catalog → Inventory, Orders → Customers).
- `ORDERCORE_CONTEXT.md`: the error contract, and the new cross-module
  contracts.
- `.claude/CLAUDE.md`: the `Shared/Domain/Exceptions`,
  `Shared/Application/Exceptions` and `Shared/Presentation/ExceptionHandling`
  convention (replacing the "once a shared exception-handling convention
  exists" note with the convention itself), and CORS configuration.
- `README.md`: storefront endpoints and the checkout flow.

## Commits

One per stage, Conventional Commits, e.g.
`feat(shared): add ProblemDetails error contract and CORS`,
`feat(inventory): expose stock availability query`,
`feat(catalog): paged storefront listing and product detail by slug`,
`feat(customers): return full addresses and single-address lookup`,
`feat(payments): record the chosen payment method`,
`feat(orders): single-step idempotent checkout and cart quote`,
`feat(orders): enriched order views and status history`,
`docs: document storefront MVP API` — plus this spec/plan as its own
`docs(storefront): ...` commit before Stage 1.
