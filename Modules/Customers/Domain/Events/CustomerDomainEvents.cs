using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Customers.Domain.Events;

/// <summary>
/// Domain events raised by the <see cref="Entities.Customer"/> aggregate —
/// see 02-customers.md and the shared kernel's remarks on
/// <see cref="IDomainEvent"/> for how these differ from Integration Events.
/// </summary>
public sealed record CustomerRegistered(Guid EventId, DateTimeOffset OccurredAt, Guid CustomerId, string Email)
    : IDomainEvent;

public sealed record CustomerAddressAdded(Guid EventId, DateTimeOffset OccurredAt, Guid CustomerId, Guid AddressId)
    : IDomainEvent;
