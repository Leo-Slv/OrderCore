# Project Architecture

OrderCore is a modular monolith built with ASP.NET Core and .NET 10.

The codebase follows Clean Architecture and Domain-Driven Design principles,
organized primarily by business module, mirroring the CourseCore project
layout.

Business functionality belongs under:

`Modules/<Module>/`

Existing modules include:

- Orders
- Customers
- Catalog
- Inventory
- Payments
- AuditLogs (cross-cutting/technical module, not a business bounded
  context — mirrors CourseCore's own AuditLogs module)

Cross-cutting functionality belongs under:

`Shared/`

Do not create new top-level architectural structures or move responsibilities
between layers without an explicit architectural reason.

Before introducing a new pattern, inspect how equivalent functionality is
implemented in existing modules (Orders is the most complete one) and prefer
the established project convention.

For the full architectural rationale (why Modular Monolith, module
boundaries, concurrency strategy, the planned Payments -> PayCore
extraction, etc.), see `docs/architecture/ORDERCORE_CONTEXT.md`.

## Module Structure

Modules generally follow this structure:

`Modules/<Module>/Application`
`Modules/<Module>/Domain`
`Modules/<Module>/Infrastructure`
`Modules/<Module>/Presentation`

Preserve this separation when adding functionality.

### Domain

The Domain layer contains business concepts and rules, including:

- Entities
- Enums
- Domain Events
- Domain Exceptions
- Policies
- Repository abstractions (e.g. `IPaymentProvider`)
- Value Objects

Business invariants should live in the domain whenever they belong to the
domain model.

Domain code must not depend on Presentation or Infrastructure concerns, nor
on ASP.NET Core or EF Core.

Do not introduce persistence-specific concerns into domain entities.

Because OrderCore is a single project (not one assembly per layer), these
boundaries are not enforced by project references. They are validated by
`OrderCore.ArchitectureTests` via NetArchTest namespace rules instead — keep
those tests passing when moving or adding code.

### Application

The Application layer coordinates use cases.

Application code may contain:

- Use Cases
- DTOs
- Application services
- Contracts
- Validation

Controllers/endpoints should delegate business operations to application use
cases instead of implementing business workflows directly.

Use cases should depend on abstractions (e.g. `IOrderRepository`,
`IProductCatalog`) rather than concrete Infrastructure implementations.

### Presentation

The Presentation layer contains HTTP/API concerns, including:

- Controllers
- Requests
- Responses
- Presenters

Controllers/endpoints should remain thin.

Do not place business rules in controllers.

Use the existing Presenter pattern for transformations between HTTP models
and application DTOs when applicable.

Do not expose persistence models through the API.

### Infrastructure

Infrastructure contains technical implementations such as persistence and
external integrations (e.g. payment providers).

Persistence (EF Core, not yet implemented) should follow the same
separation used elsewhere in the project once introduced:

- Domain entities
- Persistence models
- Mappers
- EF repositories
- EF configurations

Do not make domain entities EF Core persistence models unless an explicit
architectural decision changes this approach.

The Payments module additionally has `Infrastructure/Providers/{Fake,Stripe}`
for the `IPaymentProvider` abstraction — this is a Payments-specific
extension, not a general pattern to copy into every module without the same
justification (an external provider dependency).

## Dependency Direction

Preserve the dependency direction of the existing architecture.

In general:

Presentation -> Application -> Domain

Infrastructure implements abstractions required by the inner layers.

Domain must remain independent from Infrastructure and Presentation.

Avoid introducing dependencies between modules when an existing contract
(e.g. `IProductCatalog`) or event can preserve module boundaries. A module
must never reach directly into another module's Domain or Infrastructure
namespace.

## Dependency Injection

Each module owns its dependency registration through its own
`<Module>DependencyInjection` class (e.g. `OrdersDependencyInjection`,
`PaymentsDependencyInjection`), exposing an `Add<Module>Module()` extension
method composed in `Program.cs`.

When adding module-specific services, repositories, or use cases, register
them in the corresponding module dependency injection configuration.

Avoid registering module internals directly in `Program.cs` when they belong
to a module.

## Persistence

The project targets PostgreSQL via Entity Framework Core, but this is not
implemented yet — `Infrastructure/Persistence` folders exist as scaffolding
only. When implementing it, follow the intended shape:

Domain Entity
    ↕ Mapper
Persistence Model
    ↕ EF Core
Database

- use EF Core migrations, not schema changes applied ad hoc;
- do not run or apply production migrations automatically from application
  startup;
- do not introduce secrets or environment-specific credentials into source
  control.

## Transactions

There is no `IUnitOfWork` (or equivalent) yet. When a use case needs
transactional behavior, introduce one consistent abstraction rather than
ad-hoc transaction handling scattered across use cases, and register it the
same way other module dependencies are registered.

## Cross-Cutting Concerns

Cross-cutting concerns shared across multiple business modules belong under
`Shared/`, split the same way a module is (`Shared/Domain`,
`Shared/Application`, `Shared/Presentation`):

- `Shared/Domain` — the domain kernel: `AggregateRoot`, `Entity`,
  `IDomainEvent`.
- `Shared/Application` — technical DTOs used by more than one module, e.g.
  `PagedResult<T>`.
- `Shared/Presentation` — technical response shapes used by more than one
  module, e.g. `PagedResponse<T>`.

Do not move module-specific business logic into `Shared/` merely for reuse.

Prefer keeping business concepts within their owning module.

## API Conventions

The API is currently a minimal-API host (`Program.cs`). When adding
endpoints, place them under the owning module's
`Presentation/Controllers` (or an endpoint-mapping equivalent), not directly
in `Program.cs`.

Do not add local try/catch blocks for normal application/domain errors once
a shared exception-handling convention exists — introduce and reuse one
instead of ad hoc handling per endpoint.

## Testing

Tests are located under:

`tests/OrderCore.UnitTests/<Module>/`
`tests/OrderCore.IntegrationTests/<Module>/`
`tests/OrderCore.ArchitectureTests/`

Tests are organized by business module (not by technical layer), the same
criterion used in `Modules/`. `OrderCore.ArchitectureTests` is the
deliberate exception, since it validates rules that cut across modules and
layers.

Follow the structure and patterns of existing tests before introducing a new
testing approach.

Prefer testing observable behavior and business rules over implementation
details.

Do not change production architecture solely to make a test easier unless
there is a justified design reason.

## Documentation

Whenever a new module, or a functionality significant enough to change the
architecture (a new module, a new cross-module contract, a new persistence
strategy, etc.), is added, update the documentation in the same change —
do not treat it as a follow-up:

- Add or update a diagram under `docs/diagrams/implementation-class/` for
  the module (create `NN-<module>.md` following the existing numbered
  files' format, one `classDiagram` per module) and update the index files
  (`docs/diagrams/implementation-class.md` and
  `docs/diagrams/implementation-class/00-overview.md`) to reference it.
- Update `docs/architecture/ORDERCORE_CONTEXT.md` when the change affects
  the module list, module boundaries, or an architectural decision
  described there.
- Update this file (`.claude/claude.md`) when the change affects the
  module list or introduces a convention future work should follow (e.g. a
  new `Shared/` subfolder, a new cross-cutting pattern).

Keep the diagrams honest about what actually exists: a module diagram
should say plainly whether it documents already-implemented code or a
target/blueprint not yet built (see the note at the top of
`docs/diagrams/implementation-class/07-auditlogs.md` for the pattern).

Commit documentation updates separately from the code they document when
practical, so the history reads as one commit per concern rather than one
undifferentiated change.
