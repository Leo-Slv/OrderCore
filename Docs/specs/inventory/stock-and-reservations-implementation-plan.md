# Implementation plan — Stock and reservations

Staged the same way Customers/Catalog were: Domain → Application →
Infrastructure (EF Core) → Presentation → Docs, one commit per stage,
`dotnet build`/`dotnet test`/`dotnet format --verify-no-changes` passing
before each commit.

## Stage 1 — Domain

- `Modules/Inventory/Domain/Entities/StockItem.cs` (new): `AggregateRoot<Guid>`.
  `Create(productId, initialQuantity, productVariantId)` — no `now` needed,
  `UpdatedAt` is persistence-layer responsibility like `Customer`'s
  non-timestamped mutators. `QuantityAvailable` is a computed property
  (`QuantityOnHand - QuantityReserved`), not a stored field, despite the
  diagram listing it as a plain `+int` — same treatment as `Order.TotalAmount`.
  `Receive`, `TryReserve` (returns `false` instead of throwing when
  insufficient — that's the point of the `bool` return), `Release`,
  `Consume`, `Adjust` (validates the resulting `QuantityOnHand` never goes
  negative).
- `Modules/Inventory/Domain/Entities/InventoryReservation.cs` (extend):
  add `OrderItemId`, `ExpiresAt`, `ReleasedAt`, `ConsumedAt`. `Create` gains
  `orderItemId`. `Release`/`Consume` gain `now` (already decided in the
  diagram's own "Paridade" note) and raise `InventoryStockMovementRecorded`.
  `Expire` stays parameterless (no `expired_at` field, per the same note).
- `Modules/Inventory/Domain/Enums/StockMovementType.cs` (new).
- `Modules/Inventory/Domain/Events/InventoryDomainEvents.cs` (new):
  `InventoryStockMovementRecorded(EventId, OccurredAt, ProductId, MovementType, Quantity, ReferenceType, ReferenceId)`.
- Unit tests: `Tests/OrderCore.UnitTests/Inventory/StockItemTests.cs`,
  extend `InventoryReservationTests.cs` for the new fields/events.

## Stage 2 — Application

- `Modules/Inventory/Application/Contracts/IStockItemRepository.cs`,
  `IInventoryReservationRepository.cs` (+ `CancellationToken`, matching
  every other repository contract in the project).
- DTOs: `ReserveStockCommand`, `ReserveStockResult`, `StockItemOutput`.
- Use cases: `ReserveStockUseCase` (the retry loop from the resolved
  concurrency decision lives here), `ReleaseReservationUseCase`,
  `ConsumeReservationUseCase`, `ExpireReservationUseCase` (takes
  `IStockItemRepository` too — resolved decision), `AdjustStockUseCase`,
  `GetStockByProductIdUseCase`.
- Unit tests with fake repositories (`FakeStockItemRepository`,
  `FakeInventoryReservationRepository`), including one that simulates a
  concurrency conflict to prove the retry loop works, and the
  `Stock = 1, N concurrent requests, exactly 1 succeeds` scenario itself —
  the latter needs real DB-level concurrency, so it belongs in
  `OrderCore.IntegrationTests/Inventory` (Stage 3), matching the note
  already in `InventoryReservationTests.cs`.

## Stage 3 — Infrastructure (EF Core)

- Persistence models: `StockItemPersistenceModel`, `InventoryReservationPersistenceModel`,
  `StockMovementPersistenceModel` — expanded to every domain field the
  diagram's abbreviated shape is missing (`ProductVariantId`, `UpdatedAt`
  on `StockItemPersistenceModel`; `OrderItemId`, `ReservedAt`, `ReleasedAt`,
  `ConsumedAt` on `InventoryReservationPersistenceModel`), same reasoning
  as `CustomerAddressPersistenceModel`.
- `InventoryDbContext` + `InventoryDbContextFactory` (design-time).
- Configurations: unique index on `StockItemPersistenceModel(ProductId, ProductVariantId)`;
  `Version` as concurrency token on `StockItemPersistenceModel`.
- Mappers: `StockItemMapper`, `InventoryReservationMapper` — both need
  `Rehydrate` on their domain entities (same as `Customer.Rehydrate`) and
  an `ApplyChanges` the diagram doesn't list (same gap as `CategoryMapper`).
- `StockMovementRecorder` (`Modules/Inventory/Infrastructure/EventHandlers`):
  `IDomainEventHandler<InventoryStockMovementRecorded>`, writes a
  `StockMovementPersistenceModel` via `InventoryDbContext` (added to the
  change tracker, not saved independently — flushed by the same
  `SaveChangesAsync` call that dispatches it).
- `EfStockItemRepository`, `EfInventoryReservationRepository` — the latter
  is where `IDomainEventDispatcher.DispatchAsync` gets called for the
  first time in the codebase, right after `_dbContext.SaveChangesAsync()`
  succeeds, over every tracked aggregate's `DomainEvents`, followed by
  `ClearDomainEvents()`.
- `InventoryDependencyInjection.AddInventoryModule(configuration)`,
  registering `InventoryDbContext`, both repositories, both use-case sets,
  wired into `Program.cs`.
- Migration `InitialInventorySchema` (`dotnet ef migrations add`).
- Integration tests (`OrderCore.IntegrationTests/Inventory`, Testcontainers.PostgreSql,
  matching `EfCustomerRepositoryTests`): round-trip test, and the
  concurrency test from section 34 (`Stock = 1`, N concurrent
  `ReserveStockUseCase.ExecuteAsync` calls against the same product,
  exactly 1 succeeds) — the test this whole feature exists to make
  possible.

## Stage 4 — Presentation

- `InventoryController` ([ApiController], matching `CatalogController`):
  `GetStockByProductIdAsync`, `AdjustStockAsync`. `ReserveStock`/`Release`/
  `Consume`/`Expire` have no route in the diagram — same as
  `ChangeProductPriceUseCase` in Catalog, they stay reachable only from
  other Application-layer code (Orders, once its own adapter exists).
- `AdjustStockRequest`, `StockItemResponse`, `StockItemPresenter`.

## Stage 5 — Docs

- Mark `04-inventory.md` as implemented (like `02-customers.md`/`03-catalog.md`),
  documenting every deviation listed above.
- Update `ORDERCORE_CONTEXT.md`'s Banco de dados section (Inventory joins
  Customers/Catalog/Orders) and note the domain-event dispatch point.
- Update `claude.md`'s Persistence section the same way, and add a short
  note under a (new) Domain Events section or the Persistence section
  about where `IDomainEventDispatcher.DispatchAsync` is actually called
  now that it has a first real caller.
- Update `README.md` per the new Implementation Workflow's Docs step.
