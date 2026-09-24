using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

/// <summary>
/// Stable, versioned contracts for cross-process communication (section 19).
/// These do NOT reference domain entities — a module or future service on
/// the other side of the wire (e.g. PayCore) must be able to deserialize
/// this contract without depending on OrderCore's domain model.
///
/// No longer just a placeholder: <see cref="OutboxPublisherBackgroundService"/>
/// (Infrastructure/Outbox) actually publishes these now, by calling the
/// existing <see cref="IDomainEventDispatcher"/> directly — RabbitMQ
/// (section 21) is still a later phase, so this is a deliberate, temporary
/// bridge that reuses the in-process dispatcher's plumbing instead of
/// inventing a second one for a transport that doesn't exist yet. That is
/// the only thing shared with domain events: an <see cref="IntegrationEvent"/>
/// is still conceptually cross-process (routed through the outbox table,
/// carries its own <see cref="Version"/> for schema evolution), not an
/// in-process domain fact. See
/// Docs/specs/payments/payment-processing.md's "How publish works before
/// RabbitMQ exists".
/// </summary>
public abstract record IntegrationEvent : IDomainEvent
{
    public required Guid EventId { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}

public sealed record PaymentRequested : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string IdempotencyKey { get; init; }
}

public sealed record PaymentAuthorized : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }
}

public sealed record PaymentFailed : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }

    public required string Reason { get; init; }
}

public sealed record PaymentRefunded : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required Guid PaymentId { get; init; }
}
