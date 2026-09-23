using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Catalog.Domain.Events;

/// <summary>
/// Domain events raised by the <see cref="Entities.Product"/> aggregate —
/// see 03-catalog.md and the shared kernel's remarks on
/// <see cref="IDomainEvent"/>.
/// </summary>
public sealed record ProductCreated(Guid EventId, DateTimeOffset OccurredAt, Guid ProductId) : IDomainEvent;

public sealed record ProductPriceChanged(Guid EventId, DateTimeOffset OccurredAt, Guid ProductId, decimal OldPrice, decimal NewPrice)
    : IDomainEvent;

public sealed record ProductPublished(Guid EventId, DateTimeOffset OccurredAt, Guid ProductId) : IDomainEvent;
