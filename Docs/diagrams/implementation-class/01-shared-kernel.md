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
        +IsValid(string? value)$ bool
        +Create(string value)$ Slug
        +GenerateFrom(string text)$ Slug
    }


    %% OrderCore.Api.Shared.Domain.Exceptions
    class DomainRuleViolationException {
        <<exception>>
        +string Code
    }


    %% OrderCore.Api.Shared.Application.Exceptions
    class NotFoundException {
        <<exception>>
        +string Code
    }

    class ConflictException {
        <<exception>>
        +string Code
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


    %% OrderCore.Api.Shared.Presentation.ExceptionHandling
    class IExceptionHandler {
        <<external>>
        <<interface>>
    }

    class ApiExceptionHandler {
        -IProblemDetailsService problemDetailsService
        -IHostEnvironment environment
        +TryHandleAsync(HttpContext httpContext, Exception exception) ValueTask~bool~
        +Classify(Exception exception)$ ValueTuple~int, string, string~
    }


    %% OrderCore.Api.Shared.Presentation.Cors
    class CorsExtensions {
        <<static>>
        +string StorefrontPolicy$
        +AddStorefrontCors(IServiceCollection services, IConfiguration configuration)$ IServiceCollection
        +UseStorefrontCors(IApplicationBuilder app)$ IApplicationBuilder
    }


    Entity~TId~ <|-- AggregateRoot~TId~
    IDomainEventDispatcher <|.. InProcessDomainEventDispatcher
    InProcessDomainEventDispatcher ..> IDomainEventHandler~TEvent~ : resolves via DI
    IExceptionHandler <|.. ApiExceptionHandler
    ApiExceptionHandler ..> DomainRuleViolationException : 400
    ApiExceptionHandler ..> NotFoundException : 404
    ApiExceptionHandler ..> ConflictException : 409

```

## Quem usa o quê

- `AggregateRoot<TId>` é a base de `Customer`, `Product`/`Category`, `StockItem`/`InventoryReservation`, `Order` e `Payment` — ver o diagrama de cada módulo.
- `Address` é usada por `CustomerAddress` (Customers) e por `Order.ShippingAddress`/`Order.BillingAddress` (Orders).
- `Slug` é usada por `Product` e `Category` (Catalog).
- `IDomainEventDispatcher`/`IDomainEventHandler<T>` são implementados por `EfOrderRepository` (dispara após salvar) e consumidos pelo `OrderStatusHistoryProjector` — ver [05-orders.md](05-orders.md).
- `Slug.IsValid` é usado por `GetProductBySlugUseCase` (Catalog) para tratar um slug mal formado vindo da rota como "não encontrado", não como erro de validação.

## Contrato de erros

A convenção única de tratamento de erros da API (em vez de try/catch por endpoint). Casos de uso e agregados lançam exceções tipadas, cada uma com um `Code` estável em snake_case; `ApiExceptionHandler` (registrado com `AddProblemDetails`/`AddExceptionHandler` + `UseExceptionHandler` no `Program.cs`) as transforma em `ProblemDetails` (RFC 7807) com a extensão `code`:

| Exceção | Status | `code` |
|---|---|---|
| `DomainRuleViolationException` (Domain — invariante ou máquina de estados) | 400 | o próprio (`invalid_order_state`, `mixed_currencies`, …) |
| `NotFoundException` (Application) | 404 | o próprio (`order_not_found`, `address_not_found`, …) |
| `ConflictException` (Application; não é `sealed`, ex.: `StockConcurrencyConflictException` do Inventory herda dela) | 409 | o próprio (`insufficient_stock`, `price_changed`, …) |
| `DbUpdateConcurrencyException` (EF Core) | 409 | `concurrency_conflict` |
| `ArgumentException` (o que os `Create`/value objects já lançam) | 400 | `validation_error` |
| qualquer outra | 500 | `internal_error`, sem detalhe fora de Development |

`DomainRuleViolationException` fica em `Shared/Domain` para que o Domain de qualquer módulo possa lançá-la sem depender de nada fora do kernel. Os `[ProducesResponseType]` de erro de cada controller declaram `typeof(ProblemDetails)`.

## CORS

`CorsExtensions` registra uma política nomeada (`Storefront`) cujas origens vêm só de `Cors:AllowedOrigins` na configuração (`http://localhost:3000` no `appsettings.json`); lista vazia ou ausente não libera nenhuma origem.
