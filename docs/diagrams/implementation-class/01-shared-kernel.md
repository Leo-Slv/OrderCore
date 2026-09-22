# Shared kernel

Base usada por todos os módulos: identidade/igualdade de entidades, agregados com eventos de domínio, value objects reutilizáveis e o mecanismo que despacha os eventos de domínio para quem precisa reagir a eles (ex.: o projetor de histórico de status do Orders — ver [05-orders.md](05-orders.md)).

```mermaid

classDiagram
    direction LR

    %% OrderCore.Api.Shared.Domain
    class Entity~TId~ {
        +TId Id
        +Equals(object obj) bool
        +GetHashCode() int
    }

    class AggregateRoot~TId~ {
        +IReadOnlyCollection~IDomainEvent~ DomainEvents
        +int Version
        +ClearDomainEvents() void
        #Raise(IDomainEvent domainEvent) void
        #IncrementVersion() void
    }

    class IDomainEvent {
        <<interface>>
        +Guid EventId
        +DateTimeOffset OccurredAt
    }


    %% OrderCore.Api.Shared.Domain.ValueObjects
    class Slug {
        +string Value
        +Create(string value)$ Slug
    }

    class Address {
        +string Street
        +string Number
        +string? Complement
        +string Neighborhood
        +string City
        +string State
        +string PostalCode
        +string Country
        +Create(...)$ Address
    }


    %% OrderCore.Api.Shared.Application.Abstractions
    class IDomainEventDispatcher {
        <<interface>>
        +DispatchAsync(IReadOnlyCollection~IDomainEvent~ events) Task
    }

    class IDomainEventHandler~TEvent~ {
        <<interface>>
        +HandleAsync(TEvent domainEvent) Task
    }


    %% OrderCore.Api.Shared.Infrastructure
    class InProcessDomainEventDispatcher {
        -IServiceProvider serviceProvider
        +DispatchAsync(IReadOnlyCollection~IDomainEvent~ events) Task
    }


    Entity~TId~ <|-- AggregateRoot~TId~
    IDomainEventDispatcher <|.. InProcessDomainEventDispatcher
    InProcessDomainEventDispatcher ..> IDomainEventHandler~TEvent~ : resolves via DI

```

## Quem usa o quê

- `AggregateRoot<TId>` é a base de `Customer`, `Product`/`Category`, `StockItem`/`InventoryReservation`, `Order` e `Payment` — ver o diagrama de cada módulo.
- `Address` é usada por `CustomerAddress` (Customers) e por `Order.ShippingAddress`/`Order.BillingAddress` (Orders).
- `Slug` é usada por `Product` e `Category` (Catalog).
- `IDomainEventDispatcher`/`IDomainEventHandler<T>` são implementados por `EfOrderRepository` (dispara após salvar) e consumidos pelo `OrderStatusHistoryProjector` — ver [05-orders.md](05-orders.md).
