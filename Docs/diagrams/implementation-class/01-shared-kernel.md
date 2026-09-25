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

    class UnauthorizedException {
        <<exception>>
        +string Code
    }


    %% OrderCore.Api.Shared.Application.Abstractions (authentication)
    class ICurrentUser {
        <<interface>>
        +Guid? UserId
        +Guid? CustomerId
        +string? Role
        +bool IsAuthenticated
        +bool IsAdmin
    }

    class UserRoles {
        <<static>>
        +string Customer$
        +string Admin$
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


    class ProblemDetailsDefaults {
        <<static>>
        +AddDefaultCode(ProblemDetailsContext context)$ void
        +DefaultCodeFor(int status)$ string?
    }


    %% OrderCore.Api.Shared.Presentation.Authentication
    class OrderCoreClaimTypes {
        <<static>>
        +string UserId$
        +string Email$
        +string Role$
        +string CustomerId$
    }

    class HttpContextCurrentUser {
        -IHttpContextAccessor httpContextAccessor
    }

    class AuthorizationPolicies {
        <<static>>
        +string Customer$
        +string Admin$
        +AddOrderCoreAuthorization(IServiceCollection services)$ IServiceCollection
    }


    %% OrderCore.Api.Shared.Presentation.OpenApi
    class BearerSecurityTransformer {
        +string SchemeName$
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
    ApiExceptionHandler ..> UnauthorizedException : 401
    ApiExceptionHandler ..> ConflictException : 409
    ProblemDetailsDefaults ..> ApiExceptionHandler : same code extension
    ICurrentUser <|.. HttpContextCurrentUser
    HttpContextCurrentUser ..> OrderCoreClaimTypes : reads
    ICurrentUser ..> UserRoles
    AuthorizationPolicies ..> UserRoles
    AuthorizationPolicies ..> OrderCoreClaimTypes

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
| `UnauthorizedException` (Application — login ou refresh inválido) | 401 | o próprio (`invalid_credentials`, `invalid_refresh_token`) |
| `NotFoundException` (Application) | 404 | o próprio (`order_not_found`, `address_not_found`, …) |
| `ConflictException` (Application; não é `sealed`, ex.: `StockConcurrencyConflictException` do Inventory herda dela) | 409 | o próprio (`insufficient_stock`, `price_changed`, …) |
| `DbUpdateConcurrencyException` (EF Core) | 409 | `concurrency_conflict` |
| `ArgumentException` (o que os `Create`/value objects já lançam) | 400 | `validation_error` |
| qualquer outra | 500 | `internal_error`, sem detalhe fora de Development |

`DomainRuleViolationException` fica em `Shared/Domain` para que o Domain de qualquer módulo possa lançá-la sem depender de nada fora do kernel. Os `[ProducesResponseType]` de erro de cada controller declaram `typeof(ProblemDetails)`.

Além das exceções, `ProblemDetailsDefaults` dá um `code` às respostas de erro do próprio framework: validação de modelo (400 → `validation_error`), rota desconhecida (404 → `not_found`) e as 401/403 da autorização (`unauthenticated`/`forbidden`), que `UseStatusCodePages` transforma em `ProblemDetails`. Assim o cliente sempre pode decidir pelo `code`.

## Autenticação e autorização

Os tokens são emitidos e validados pelo módulo Identity ([08-identity.md](08-identity.md)). O que todos os módulos compartilham fica aqui:

- **`ICurrentUser`**: quem está chamando (id da conta, id do cliente, papel), lido das claims do token por `HttpContextCurrentUser`. Casos de uso e o `AuditLogService` perguntam a ele em vez de ler HTTP. Fora de uma requisição (background) é "ninguém".
- **`AuthorizationPolicies`**: `Customer` (papel `Customer` e claim `customer_id`) e `Admin`, mais uma política de fallback que exige usuário logado. Ou seja, **bloqueio por padrão**: o que não for marcado `[AllowAnonymous]` exige token, inclusive rotas que não existem (401 para anônimo, 404 para logado, para não revelar quais rotas existem). `EndpointAuthorizationTests` (testes de arquitetura) falha se alguma action não estiver classificada explicitamente.
- **`BearerSecurityTransformer`**: declara o esquema Bearer no documento OpenAPI e marca as operações protegidas com suas respostas 401/403, para o Scalar conseguir enviar o token.

## CORS

`CorsExtensions` registra uma política nomeada (`Storefront`) cujas origens vêm só de `Cors:AllowedOrigins` na configuração (`http://localhost:3000` no `appsettings.json`); lista vazia ou ausente não libera nenhuma origem.
