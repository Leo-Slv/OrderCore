namespace OrderCore.Api.Shared.Domain;

/// <summary>
/// Represents a fact that already happened in the domain (past tense).
/// Domain events are raised by aggregates and dispatched within the same
/// unit of work / transaction. They are NOT the same thing as Integration
/// Events (see Modules/Payments/Application/Contracts/IntegrationEvents),
/// which cross process/module boundaries and require a delivery guarantee
/// (Transactional Outbox).
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}
