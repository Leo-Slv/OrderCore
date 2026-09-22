namespace OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

/// <summary>
/// Stable, versioned contracts for cross-process communication (section 19).
/// These do NOT reference domain entities — a module or future service on
/// the other side of the wire (e.g. PayCore) must be able to deserialize
/// this contract without depending on OrderCore's domain model.
///
/// Today these are placeholders: while Payments still lives in-process,
/// nothing publishes them yet. They exist so the shape of the eventual
/// contract is designed deliberately (section 3, Fase 3) instead of
/// improvised at extraction time.
/// </summary>
public abstract record IntegrationEvent
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
