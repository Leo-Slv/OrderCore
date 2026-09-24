using System.Text.Json;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Outbox;

/// <summary>
/// Adds an <see cref="OutboxMessage"/> to the same <see cref="PaymentsDbContext"/>
/// instance <c>EfPaymentRepository</c> uses (both Scoped, sharing one
/// DbContext per unit of work) without saving on its own — flushed by
/// whatever `SaveChangesAsync` the calling use case runs, in the very same
/// transaction as the `Payment` write. That's the actual "transactional"
/// part of transactional outbox: the message and the state that produced
/// it can never end up out of sync.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly PaymentsDbContext _dbContext;

    public OutboxWriter(PaymentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Enqueue(IntegrationEvent integrationEvent)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = IntegrationEventTypeRegistry.NameOf(integrationEvent),
            PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAt = integrationEvent.OccurredAt,
        };

        _dbContext.OutboxMessages.Add(message);
    }
}
