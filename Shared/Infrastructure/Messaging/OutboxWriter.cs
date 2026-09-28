using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// <see cref="IOutbox"/> over one module's <c>DbContext</c>: adds the event
/// to the context's outbox table so the module's next save writes it with
/// the aggregate. Captures the current trace context (the request or the
/// message being handled) and, inside a consumer, the causing message — so
/// the chain "checkout → payment authorized → order confirmed" stays one
/// trace (the correlation id is the trace id).
/// </summary>
public sealed class OutboxWriter<TDbContext> : IOutbox
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;
    private readonly IntegrationEventRegistry _registry;
    private readonly IMessageContext _messageContext;

    public OutboxWriter(TDbContext dbContext, IntegrationEventRegistry registry, IMessageContext messageContext)
    {
        _dbContext = dbContext;
        _registry = registry;
        _messageContext = messageContext;
    }

    public void Enqueue(IntegrationEvent integrationEvent)
    {
        var contract = _registry.ContractOf(integrationEvent.GetType());
        var activity = Activity.Current;

        _dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = contract.Name,
            Version = contract.Version,
            PayloadJson = MessageEnvelope.SerializePayload(integrationEvent),
            OccurredAt = integrationEvent.OccurredAt,
            TraceParent = activity?.Id,
            TraceState = activity?.TraceStateString,
            CausationId = _messageContext.CausationId,
        });
    }
}
