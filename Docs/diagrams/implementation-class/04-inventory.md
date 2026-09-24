# Módulo Inventory

Saldo de estoque (`StockItem`) e reserva explícita por item de pedido (`InventoryReservation`, já existente no código). Base: [Shared kernel](01-shared-kernel.md) (`AggregateRoot<Guid>`).

Como [02-customers.md](02-customers.md) e [03-catalog.md](03-catalog.md), este módulo já está **implementado** de ponta a ponta (Domain, Application, Infrastructure/EF Core e Presentation) — não é mais um blueprint futuro. Ver `Docs/specs/inventory/stock-and-reservations.md` para o spec completo e as decisões em aberto resolvidas antes da implementação. Diferenças entre este diagrama e o código, todas documentadas nos comentários das classes correspondentes:

- `StockItem.Create` recebe um `now` explícito (como `Customer.Create`), já que `UpdatedAt` precisa de um valor.
- **Disponibilidade para outros módulos** (MVP do storefront, `Docs/specs/storefront/storefront-api-mvp.md`, etapa 2): `GetStockAvailabilityUseCase` devolve, numa só consulta (`IStockItemRepository.ListByProductIdsAsync`, sem rastreamento do EF para não interferir na unidade de trabalho), a quantidade disponível e `StockItem.IsLowStock` (`0 < disponível <= ReorderLevel`) de vários produtos; produto sem registro de estoque sai como 0 disponível. É o único ponto de entrada que Catalog e Orders usam para saber de estoque. Como nada define `ReorderLevel` ainda (fica 0), `IsLowStock` hoje é sempre `false`.
- `StockConcurrencyConflictException` passou a herdar de `ConflictException` (shared kernel), com código `concurrency_conflict`: esgotadas as tentativas de `ReserveStockUseCase`, chega ao cliente como 409.
- `IStockItemRepository`/`IInventoryReservationRepository` **não têm** `SaveChangesAsync`: toda operação que muda estado mexe nos dois agregados (`StockItem` e `InventoryReservation`) na mesma chamada, e dois `SaveChangesAsync` separados seriam duas transações SQL diferentes, não uma unidade atômica. Introduzido `IUnitOfWork` (novo, não estava no diagrama) — ver a seção Transactions do `claude.md`.
- `ExpireReservationUseCase` também depende de `IStockItemRepository`: o diagrama original só listava `IInventoryReservationRepository`, o que deixaria a quantidade reservada presa no `StockItem` para sempre depois de uma reserva expirar.
- `StockItemPersistenceModel`/`InventoryReservationPersistenceModel` guardam todos os campos das respectivas entidades de domínio (`ProductVariantId`/`UpdatedAt`/`Version` no primeiro; `ReservedAt`/`ReleasedAt`/`ConsumedAt`/`Version` no segundo), não só o subconjunto abreviado do diagrama — mesma razão de `CustomerAddressPersistenceModel`.
- `StockItemMapper`/`InventoryReservationMapper` ganharam um `ApplyChanges` que o diagrama não lista — mesmo motivo de `CategoryMapper`.
- `EfStockItemRepository`/`EfInventoryReservationRepository` implementam uma interface interna `IPendingChangesTracker` (não estava no diagrama) em vez de expor `SaveChangesAsync` — é o que permite a `InventoryUnitOfWork` coordenar os dois em um único save atômico.
- `StockMovementRecorder` é um `IDomainEventHandler<InventoryStockMovementRecorded>` de verdade (não um serviço com `RecordAsync` chamado diretamente pelos use cases) — o primeiro handler de domain event real do projeto, despachado por `InventoryUnitOfWork.SaveChangesAsync` através do `InProcessDomainEventDispatcher` do shared kernel, que até esta feature nunca era efetivamente chamado por ninguém.
- `InventoryStockMovementRecorded` (novo domain event, não estava no diagrama) é levantado por `InventoryReservation.Create`/`Release`/`Consume` — as três operações que o diagrama já ligava a `StockMovementRecorder`. `StockItem.Receive`/`Adjust` não levantam esse evento: não existe `ReceiveStockUseCase` e `AdjustStockUseCase` não tem seta pontilhada para `StockMovementRecorder` no diagrama original — `Inbound`/`Outbound`/`Adjustment` continuam definidos em `StockMovementType` mas não usados por enquanto (mesmo tratamento de `ChangeProductPriceUseCase` sem rota em Catalog).
- Corrigido também, ao escrever o teste de concorrência desta feature: `CustomerMapper`/`ProductMapper`/`CategoryMapper`/`OrderMapper.ApplyChanges` nunca sincronizavam `Version` — o token de concorrência otimista nunca incrementava de fato após um update, tornando a checagem do EF Core um no-op em todo o projeto. Corrigido em todos os quatro (commit separado, fora do escopo do Inventory).

```mermaid

classDiagram
    direction LR

    class AggregateRoot~TId~ {
        <<external>>
    }

    class IDomainEvent {
        <<external>>
    }

    class IDomainEventHandler~TEvent~ {
        <<external>>
    }

    class IDomainEventDispatcher {
        <<external>>
    }

    %% OrderCore.Api.Modules.Inventory.Domain.Entities
    class StockItem {
        +Guid ProductId
        +Guid? ProductVariantId
        +int QuantityOnHand
        +int QuantityReserved
        +int QuantityAvailable
        +int ReorderLevel
        +bool IsLowStock
        +DateTimeOffset UpdatedAt
        +Create(Guid productId, int initialQuantity, Guid? productVariantId, DateTimeOffset now)$ StockItem
        +Receive(int quantity) void
        +TryReserve(int quantity) bool
        +Release(int quantity) void
        +Consume(int quantity) void
        +Adjust(int quantity, string reason) void
    }

    class InventoryReservation {
        +Guid ProductId
        +Guid OrderId
        +Guid OrderItemId
        +int Quantity
        +ReservationStatus Status
        +DateTimeOffset ReservedAt
        +DateTimeOffset? ExpiresAt
        +DateTimeOffset? ReleasedAt
        +DateTimeOffset? ConsumedAt
        +Create(Guid productId, Guid orderId, Guid orderItemId, int quantity, DateTimeOffset now)$ InventoryReservation
        +Release(DateTimeOffset now) void
        +Consume(DateTimeOffset now) void
        +Expire() void
    }


    %% OrderCore.Api.Modules.Inventory.Domain.Enums
    class ReservationStatus {
        <<enumeration>>
        Reserved
        Released
        Consumed
        Expired
    }

    class StockMovementType {
        <<enumeration>>
        Inbound
        Outbound
        Adjustment
        ReservationCreated
        ReservationReleased
        ReservationConsumed
    }


    %% OrderCore.Api.Modules.Inventory.Domain.Events
    class InventoryStockMovementRecorded {
        +Guid EventId
        +DateTimeOffset OccurredAt
        +Guid ProductId
        +StockMovementType MovementType
        +int Quantity
        +string ReferenceType
        +Guid ReferenceId
    }


    %% OrderCore.Api.Modules.Inventory.Application.Contracts
    class IStockItemRepository {
        <<interface>>
        +GetByProductIdAsync(Guid productId) Task~StockItem?~
        +ListByProductIdsAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyList~StockItem~~
        +AddAsync(StockItem stockItem) Task
    }

    class IInventoryReservationRepository {
        <<interface>>
        +GetByIdAsync(Guid reservationId) Task~InventoryReservation?~
        +ListByOrderIdAsync(Guid orderId) Task~IReadOnlyList~InventoryReservation~~
        +AddAsync(InventoryReservation reservation) Task
    }

    class IUnitOfWork {
        <<interface>>
        +SaveChangesAsync() Task
    }

    class StockConcurrencyConflictException {
        <<exception>>
        +string ErrorCode$
    }

    class ConflictException {
        <<external>>
    }

    note for ConflictException "Shared kernel — ver 01-shared-kernel.md"


    %% OrderCore.Api.Modules.Inventory.Application.DTOs
    class ReserveStockCommand {
        +Guid ProductId
        +Guid OrderId
        +Guid OrderItemId
        +int Quantity
    }

    class ReserveStockResult {
        +Guid? ReservationId
        +bool Succeeded
    }

    class StockItemOutput {
        +Guid ProductId
        +int QuantityOnHand
        +int QuantityAvailable
    }

    class StockAvailabilityOutput {
        +Guid ProductId
        +int QuantityAvailable
        +bool IsLowStock
    }


    %% OrderCore.Api.Modules.Inventory.Application.UseCases
    class ReserveStockUseCase {
        -IStockItemRepository stockItems
        -IInventoryReservationRepository reservations
        -IUnitOfWork unitOfWork
        +ExecuteAsync(ReserveStockCommand command) Task~ReserveStockResult~
    }

    class ReleaseReservationUseCase {
        -IStockItemRepository stockItems
        -IInventoryReservationRepository reservations
        -IUnitOfWork unitOfWork
        +ExecuteAsync(Guid reservationId) Task
    }

    class ConsumeReservationUseCase {
        -IStockItemRepository stockItems
        -IInventoryReservationRepository reservations
        -IUnitOfWork unitOfWork
        +ExecuteAsync(Guid reservationId) Task
    }

    class ExpireReservationUseCase {
        -IInventoryReservationRepository reservations
        -IStockItemRepository stockItems
        -IUnitOfWork unitOfWork
        +ExecuteAsync(Guid reservationId) Task
    }

    class AdjustStockUseCase {
        -IStockItemRepository stockItems
        -IUnitOfWork unitOfWork
        +ExecuteAsync(Guid productId, int quantity, string reason) Task~StockItemOutput~
    }

    class GetStockByProductIdUseCase {
        -IStockItemRepository stockItems
        +ExecuteAsync(Guid productId) Task~StockItemOutput~
    }

    class GetStockAvailabilityUseCase {
        -IStockItemRepository stockItems
        +ExecuteAsync(IReadOnlyCollection~Guid~ productIds) Task~IReadOnlyList~StockAvailabilityOutput~~
    }


    %% OrderCore.Api.Modules.Inventory
    class InventoryDependencyInjection {
        <<static>>
        +AddInventoryModule(IServiceCollection services)$ IServiceCollection
    }


    %% OrderCore.Api.Modules.Inventory.Infrastructure.Persistence
    class StockItemPersistenceModel {
        +Guid Id
        +Guid ProductId
        +Guid? ProductVariantId
        +int QuantityOnHand
        +int QuantityReserved
        +int ReorderLevel
        +DateTimeOffset UpdatedAt
        +int Version
    }

    class InventoryReservationPersistenceModel {
        +Guid Id
        +Guid ProductId
        +Guid OrderId
        +Guid OrderItemId
        +int Quantity
        +string Status
        +DateTimeOffset ReservedAt
        +DateTimeOffset? ExpiresAt
        +DateTimeOffset? ReleasedAt
        +DateTimeOffset? ConsumedAt
        +int Version
    }

    class StockMovementPersistenceModel {
        +Guid Id
        +Guid ProductId
        +string MovementType
        +int Quantity
        +string? ReferenceType
        +Guid? ReferenceId
        +DateTimeOffset CreatedAt
    }

    class StockItemMapper {
        +ToDomain(StockItemPersistenceModel model) StockItem
        +ToPersistence(StockItem domain) StockItemPersistenceModel
        +ApplyChanges(StockItem domain, StockItemPersistenceModel model) void
    }

    class InventoryReservationMapper {
        +ToDomain(InventoryReservationPersistenceModel model) InventoryReservation
        +ToPersistence(InventoryReservation domain) InventoryReservationPersistenceModel
        +ApplyChanges(InventoryReservation domain, InventoryReservationPersistenceModel model) void
    }

    class InventoryDbContext {
        +DbSet~StockItemPersistenceModel~ StockItems
        +DbSet~InventoryReservationPersistenceModel~ Reservations
        +DbSet~StockMovementPersistenceModel~ StockMovements
        +SaveChangesAsync() Task~int~
    }

    class IPendingChangesTracker {
        <<interface>>
        <<internal>>
        +ApplyPendingChanges() void
        +CollectAndClearDomainEvents() IReadOnlyCollection~IDomainEvent~
        +ForgetTrackedEntries() void
    }

    class EfStockItemRepository {
        -InventoryDbContext dbContext
    }

    class EfInventoryReservationRepository {
        -InventoryDbContext dbContext
    }

    class InventoryUnitOfWork {
        -InventoryDbContext dbContext
        -EfStockItemRepository stockItemRepository
        -EfInventoryReservationRepository reservationRepository
        -IDomainEventDispatcher domainEventDispatcher
        +SaveChangesAsync() Task
    }


    %% OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers
    class StockMovementRecorder {
        -InventoryDbContext dbContext
        +HandleAsync(InventoryStockMovementRecorded domainEvent) Task
    }


    %% OrderCore.Api.Modules.Inventory.Presentation
    class InventoryController {
        -GetStockByProductIdUseCase getStockByProductIdUseCase
        -AdjustStockUseCase adjustStockUseCase
        +GetStockByProductIdAsync(Guid productId) Task~ActionResult~StockItemResponse~~
        +AdjustStockAsync(Guid productId, AdjustStockRequest request) Task~ActionResult~StockItemResponse~~
    }

    class AdjustStockRequest {
        +int Quantity
        +string Reason
    }

    class StockItemResponse {
        +Guid ProductId
        +int QuantityOnHand
        +int QuantityAvailable
    }

    class StockItemPresenter {
        +ToResponse(StockItemOutput output) StockItemResponse
    }


    AggregateRoot~TId~ <|-- StockItem
    AggregateRoot~TId~ <|-- InventoryReservation
    InventoryReservation --> ReservationStatus
    IDomainEvent <|.. InventoryStockMovementRecorded
    InventoryReservation ..> InventoryStockMovementRecorded : raises

    ReserveStockUseCase --> IStockItemRepository
    ReserveStockUseCase --> IInventoryReservationRepository
    ReserveStockUseCase --> IUnitOfWork
    ReleaseReservationUseCase --> IStockItemRepository
    ReleaseReservationUseCase --> IInventoryReservationRepository
    ReleaseReservationUseCase --> IUnitOfWork
    ConsumeReservationUseCase --> IStockItemRepository
    ConsumeReservationUseCase --> IInventoryReservationRepository
    ConsumeReservationUseCase --> IUnitOfWork
    ExpireReservationUseCase --> IInventoryReservationRepository
    ExpireReservationUseCase --> IStockItemRepository
    ExpireReservationUseCase --> IUnitOfWork
    AdjustStockUseCase --> IStockItemRepository
    AdjustStockUseCase --> IUnitOfWork
    GetStockByProductIdUseCase --> IStockItemRepository
    GetStockAvailabilityUseCase --> IStockItemRepository
    GetStockAvailabilityUseCase ..> StockAvailabilityOutput : returns
    IUnitOfWork ..> StockConcurrencyConflictException : throws on conflict
    ConflictException <|-- StockConcurrencyConflictException

    IStockItemRepository <|.. EfStockItemRepository
    IInventoryReservationRepository <|.. EfInventoryReservationRepository
    IPendingChangesTracker <|.. EfStockItemRepository
    IPendingChangesTracker <|.. EfInventoryReservationRepository
    EfStockItemRepository --> InventoryDbContext
    EfInventoryReservationRepository --> InventoryDbContext
    IUnitOfWork <|.. InventoryUnitOfWork
    InventoryUnitOfWork --> EfStockItemRepository
    InventoryUnitOfWork --> EfInventoryReservationRepository
    InventoryUnitOfWork --> IDomainEventDispatcher : dispatches after save
    IDomainEventHandler~TEvent~ <|.. StockMovementRecorder
    StockMovementRecorder --> InventoryDbContext

    InventoryDependencyInjection --> ReserveStockUseCase : registers

    InventoryController --> GetStockByProductIdUseCase
    InventoryController --> AdjustStockUseCase
    InventoryController --> StockItemPresenter
    StockItemPresenter --> StockItemResponse

```

## Paridade com `Docs/database/OrderCore_Modelagem_Banco_Backend.docx`

O documento de modelagem de banco já especificava `product_variant_id` em `STOCK_ITEMS` (seção 5.5, para estoque por variação) e `released_at`/`consumed_at` em `INVENTORY_RESERVATIONS` (seção 6.5), mas nenhum dos dois tinha chegado a este diagrama. Adicionados agora — `Release`/`Consume` passam a receber `now` para poder preencher o respectivo timestamp (`Expire` não precisa, já que o documento não define um `expired_at` separado: o status `Expired` já é suficiente).

## Consumido por outros módulos

- **Orders** aciona `ReserveStockUseCase`, `ReleaseReservationUseCase`, `ConsumeReservationUseCase` e `GetStockAvailabilityUseCase` de dentro de um `InventoryServiceAdapter` que implementa o `IInventoryService` do próprio módulo Orders — ver [05-orders.md](05-orders.md).
- **Catalog** chama `GetStockAvailabilityUseCase` de dentro de um `InventoryStockAvailabilityAdapter` (implementa o `IStockAvailabilityProvider` do Catalog) e converte a quantidade em estado (`InStock`/`LowStock`/`OutOfStock`) — ver [03-catalog.md](03-catalog.md). `InventoryReservation.OrderId`/`OrderItemId` guardam apenas os ids, sem referenciar `Order`/`OrderItem` diretamente.
