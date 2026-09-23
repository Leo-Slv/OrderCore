# Stock and reservations (Inventory module)

## What

Implement the Inventory module end-to-end per
[04-inventory.md](../../diagrams/implementation-class/04-inventory.md):

- A `StockItem` aggregate tracking on-hand/reserved/available quantity per
  product (optionally per product variant), with receiving, reserving,
  releasing, consuming and manually adjusting stock.
- Extending the existing `InventoryReservation` aggregate with the fields
  the diagram adds (`OrderItemId`, `ExpiresAt`, `ReleasedAt`, `ConsumedAt`)
  and threading `now` through `Release`/`Consume`.
- The Application layer: `IStockItemRepository`/`IInventoryReservationRepository`
  contracts, DTOs, and the six use cases (`ReserveStock`, `ReleaseReservation`,
  `ConsumeReservation`, `ExpireReservation`, `AdjustStock`, `GetStockByProductId`).
- EF Core persistence (`InventoryDbContext`, persistence models, mappers,
  `EfStockItemRepository`/`EfInventoryReservationRepository`, migration),
  following the shape established in `Modules/Customers/Infrastructure/Persistence`.
- A `StockMovementRecorder` writing an audit trail row
  (`StockMovementPersistenceModel`) for every stock-affecting operation.
- `InventoryController` (MVC, matching `CustomersController`/`CatalogController`)
  exposing `GET`/`POST` for reading stock and manually adjusting it.

## Why

Inventory is called out in the project context (sections 11/12/34) as the
module carrying the system's central concurrency guarantee: with
`Stock = 1` and concurrent reservation requests, exactly one must succeed.
Treating stock reservation as a first-class, stateful concept
(`InventoryReservation`, not a bare `stock -= quantity`) is what makes the
later compensation flow possible (order created → inventory reserved →
payment failed → inventory released).

This module also unblocks Orders' own inventory integration
(`InventoryServiceAdapter`/`IInventoryService`, described in
[05-orders.md](../../diagrams/implementation-class/05-orders.md)), the
same way `Catalog.IProductRepository` unblocked `ProductCatalogAdapter`.

## Open decisions (resolved)

- **Concurrency strategy for the central race ("Stock=1, concurrent
  reservations, exactly one wins")**: optimistic concurrency via
  `StockItem`'s `Version` column (same `IsConcurrencyToken()` pattern as
  `Customer`/`Product`/`Category`/`Order`), with `ReserveStockUseCase`
  retrying up to 3 times (reload + retry `TryReserve`, no backoff) on a
  `DbUpdateConcurrencyException` before giving up.
- **`StockMovementRecorder`**: a real `IDomainEventHandler<T>`, dispatched
  through the existing (until now unused) `InProcessDomainEventDispatcher`.
  This is the first place in the codebase that actually calls
  `IDomainEventDispatcher.DispatchAsync` — `EfInventoryReservationRepository.SaveChangesAsync`
  dispatches the tracked aggregates' domain events after a successful save,
  establishing the dispatch point future modules should copy (documented
  in claude.md's Persistence section). `InventoryReservation` raises one
  `InventoryStockMovementRecorded` event (carrying the `StockMovementType`)
  from `Create`/`Release`/`Consume` — the three operations 04-inventory.md
  itself wires to `StockMovementRecorder`. `StockItem.Receive`/`Adjust`
  don't raise one: there is no `ReceiveStockUseCase` in the diagram, and
  `AdjustStockUseCase` has no dotted arrow to `StockMovementRecorder`
  either — `Inbound`/`Outbound`/`Adjustment` stay defined in
  `StockMovementType` but unused for now (same as `ChangeProductPriceUseCase`
  having no route in Catalog).
- **`ExpireReservationUseCase` and `StockItem`**: the diagram wires this
  use case to `IInventoryReservationRepository` only, which would leave an
  expired reservation's quantity permanently stuck as "reserved" on the
  `StockItem`. Deviating from the diagram: `ExpireReservationUseCase` also
  takes `IStockItemRepository` and calls `StockItem.Release` — the same
  class of necessary gap-filling as `Customer.Create` gaining `now`.
- **Cross-aggregate atomicity (`IUnitOfWork`)**: `ReserveStock`/`Release`/
  `Consume`/`ExpireReservationUseCase` all mutate both `StockItem` and
  `InventoryReservation` in one operation. Every prior repository
  (`ICustomerRepository`, `IProductRepository`, `IOrderRepository`, ...)
  has its own `SaveChangesAsync`, which only works because each use case
  so far ever touched one aggregate root per call. That stops being true
  here, and calling two separate `SaveChangesAsync`s would be two separate
  SQL transactions — not atomic. Per claude.md's Transactions section
  ("introduce one consistent abstraction... register it the same way
  other module dependencies are registered"), `IStockItemRepository`/
  `IInventoryReservationRepository` drop `SaveChangesAsync` (deviating
  from the diagram) in favor of a single `IUnitOfWork.SaveChangesAsync`
  shared by both, which is also where `IDomainEventDispatcher.DispatchAsync`
  is actually called for the first time in the codebase.

## Out of scope

- `IInventoryService`/`InventoryServiceAdapter` and any other Orders-side
  change — that lives in `Modules/Orders/Infrastructure/Adapters` and is
  05-orders.md's concern, not this module's. This spec only builds what
  04-inventory.md itself describes: `IStockItemRepository`/
  `IInventoryReservationRepository` as the contracts Orders will eventually
  consume through its own adapter.
- ADR-009 (optimistic concurrency) as a formally written ADR — out of
  scope for this feature; the concurrency *behavior* it will eventually
  document is in scope (see open decisions below), but writing the ADR
  document itself is a separate task.
