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
extraction, etc.), see `Docs/architecture/ORDERCORE_CONTEXT.md`.

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

Persistence (EF Core — see the Persistence section below for which
modules already have it) should follow the same separation across every
module:

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

The project targets PostgreSQL via Entity Framework Core. All five business
modules (Customers, Catalog, Orders, Inventory, Payments) have
`Infrastructure/Persistence` implemented end to end — `AuditLogs` (the
cross-cutting/technical module) is still scaffolding only. Follow their
shape when implementing persistence for another module:

Domain Entity
    ↕ Mapper
Persistence Model
    ↕ EF Core
Database

- one `<Module>DbContext` per module (not a single project-wide
  `ApplicationDbContext`), each one only mapping its own module's tables —
  keeps module isolation at the persistence layer too;
- `<Entity>Mapper.ToDomain`/`ToPersistence`/`ApplyChanges` translate
  between the domain entity and its persistence model — never map EF Core
  directly onto the domain entity;
- a domain entity that needs to be reconstructed from storage gets an
  `internal static Rehydrate(...)` factory, separate from `Create(...)`:
  `Create` validates creation invariants and raises domain events,
  `Rehydrate` should do neither;
- the concrete repository (`Ef<Entity>Repository`) keeps its own mapping
  from the domain instance it handed out (via `GetByIdAsync` etc.) to the
  EF-tracked persistence model, and reconciles the two with `ApplyChanges`
  right before `SaveChangesAsync` — the repository interface has no
  explicit `UpdateAsync` (same shape as `IOrderRepository`);
- child-collection add/update/remove reconciliation lives in
  `Shared/Infrastructure/Persistence/ChildCollectionReconciler`, reused by
  every `<Entity>Mapper.ApplyChanges` — do not re-implement it per module;
- **`ApplyChanges` must set `model.Version = domain.Version`.** A real bug
  slipped into all four existing mappers before Inventory's concurrency
  test caught it: `Version` was only ever set in `ToPersistence` (insert
  time), never in `ApplyChanges` (update time), which made EF Core's
  optimistic-concurrency check a no-op after the first update — the
  column never actually changed, so a concurrent writer's original-value
  comparison kept matching. Every `ApplyChanges` must copy `Version`
  across, full stop;
- when a use case mutates more than one aggregate root in the same
  operation (see Transactions below), route the save through that
  module's `IUnitOfWork` instead of each repository's own
  `SaveChangesAsync` — see `Modules/Inventory/Application/Contracts/IUnitOfWork.cs`
  and `InventoryUnitOfWork`;
- if an aggregate's domain events need to actually reach an
  `IDomainEventHandler<T>` (shared kernel), dispatch them via
  `IDomainEventDispatcher.DispatchAsync` right after the save succeeds —
  `InventoryUnitOfWork.SaveChangesAsync` is the first (and, for modules
  without an `IUnitOfWork`, the reference) place this actually happens;
  domain events raised but never dispatched (true for every module before
  Inventory) just sit unused on the aggregate until `ClearDomainEvents()`;
- use EF Core migrations, not schema changes applied ad hoc;
- do not run or apply production migrations automatically from application
  startup;
- do not introduce secrets or environment-specific credentials into source
  control.

## Transactions

`Modules/Inventory/Application/Contracts/IUnitOfWork.cs` (implemented by
`InventoryUnitOfWork`) is the first `IUnitOfWork` in the project, added
because `ReserveStock`/`Release`/`Consume`/`ExpireReservationUseCase` all
mutate two aggregate roots (`StockItem` and `InventoryReservation`) in one
operation — a separate `SaveChangesAsync` per repository would be two
separate SQL transactions, not one atomic unit. When another module's use
case needs the same (touches more than one aggregate root per operation),
introduce a `IUnitOfWork` the same way rather than ad-hoc transaction
handling scattered across use cases, and register it the same way other
module dependencies are registered. A module whose use cases only ever
touch one aggregate root per operation does not need one — keep using each
repository's own `SaveChangesAsync`, matching `ICustomerRepository`/
`IProductRepository`/`IOrderRepository`.

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

`Tests/OrderCore.UnitTests/<Module>/`
`Tests/OrderCore.IntegrationTests/<Module>/`
`Tests/OrderCore.ArchitectureTests/`

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

- Add or update a diagram under `Docs/diagrams/implementation-class/` for
  the module (create `NN-<module>.md` following the existing numbered
  files' format, one `classDiagram` per module) and update the index files
  (`Docs/diagrams/implementation-class.md` and
  `Docs/diagrams/implementation-class/00-overview.md`) to reference it.
- Update `Docs/architecture/ORDERCORE_CONTEXT.md` when the change affects
  the module list, module boundaries, or an architectural decision
  described there.
- Update this file (`.claude/claude.md`) when the change affects the
  module list or introduces a convention future work should follow (e.g. a
  new `Shared/` subfolder, a new cross-cutting pattern).

Keep the diagrams honest about what actually exists: a module diagram
should say plainly whether it documents already-implemented code or a
target/blueprint not yet built (see the note at the top of
`Docs/diagrams/implementation-class/07-auditlogs.md` for the pattern).

Commit documentation updates separately from the code they document when
practical, so the history reads as one commit per concern rather than one
undifferentiated change.

## Implementation Workflow

Follow the same process used in the CourseCore backend repo, one feature at
a time:

1. **Spec (WHAT/WHY only)** at `Docs/specs/<domain>/<feature>.md` — no
   implementation detail.
2. **Resolve open decisions explicitly.** If something can't be inferred
   from the existing code, this file, or the reference project, list the
   questions and ask before proceeding — don't assume silently.
3. **Implementation plan (HOW)** at
   `Docs/specs/<domain>/<feature>-implementation-plan.md`.
4. **Implement.**
5. **Tests.** Fix/add tests until `dotnet build` (compilation, which is
   also this project's type check), `dotnet test` (all three projects —
   `Tests/OrderCore.UnitTests`, `Tests/OrderCore.IntegrationTests`,
   `Tests/OrderCore.ArchitectureTests`), and
   `dotnet format OrderCore.sln --verify-no-changes` (formatting/lint) all
   pass.
6. **Docs.** Update `README.md` (and this file, if the architecture
   changed) to reflect the new feature — see the Documentation section
   above for the diagram/context-doc updates that also apply. This
   includes the class diagram itself: any class, attribute, method
   signature, or dependency that implementation ended up needing but the
   diagram didn't have (a `now` parameter, an added field, a whole new
   abstraction like `IUnitOfWork`) gets added back into the diagram before
   the feature is considered done — never left as an undocumented
   difference between the diagram and the code. This is not optional or
   deferrable to a later pass; it happens at the end of every feature's
   workflow, every time, no matter how small the deviation looks.
7. **Commit.** Conventional Commits, in English, separated by context
   (several small commits, never one giant commit). Never add a
   `Co-Authored-By: Claude` trailer — commits are attributed to the user
   only. Never push to the remote without an explicit request.
