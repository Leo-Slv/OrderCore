namespace OrderCore.Api.Shared.Application.Messaging;

/// <summary>
/// A fact one module announces to the others through the message broker
/// (section 19): a stable, versioned contract that never references domain
/// entities, so the module on the other side of the wire — or a future
/// service like PayCore — can read it without OrderCore's domain model.
/// Each module declares its own under <c>Modules/&lt;Module&gt;/Contracts/IntegrationEvents</c>
/// and registers it with a name and version
/// (<c>MessagingRegistrationExtensions.AddIntegrationEvent</c>).
/// Deliberately not an <c>IDomainEvent</c>: domain events stay in-process,
/// integration events always go through the outbox and the broker.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Also the message id: a consumer handles each id once.</summary>
    public required Guid EventId { get; init; }

    /// <summary>The contract's schema version; an additive change keeps it, a breaking one bumps it.</summary>
    public required int Version { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
