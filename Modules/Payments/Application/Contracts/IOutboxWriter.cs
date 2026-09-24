using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

namespace OrderCore.Api.Modules.Payments.Application.Contracts;

/// <summary>
/// 06-payments.md places this under Infrastructure/Outbox, but the six use
/// cases (Application layer) all depend on it directly — leaving it there
/// would mean Application depending on Infrastructure, which
/// `OrderCore.ArchitectureTests` forbids (claude.md's Application section).
/// Moved here; the concrete `OutboxWriter` and everything else outbox-
/// shaped (`OutboxMessage`, `OutboxPublisherBackgroundService`) stay in
/// Infrastructure/Outbox exactly as diagrammed.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(IntegrationEvent integrationEvent);
}
