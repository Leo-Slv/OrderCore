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
/// <para>
/// Events are kept in the order they were enqueued. An aggregate often
/// raises several in one save with the same timestamp (checkout: order
/// created, then payment requested); the relay publishes by
/// <c>OccurredAt</c> and screens order updates by it, so an event that
/// isn't later than the previous one in this scope is moved to 1 µs after
/// it — the row and the payload alike. Otherwise "payment requested" could
/// go out, and show, before "created".
/// </para>
/// </summary>
public sealed class OutboxWriter<TDbContext> : IOutbox
    where TDbContext : DbContext
{
    /// <summary>PostgreSQL keeps microseconds, so this is the smallest step that survives saving.</summary>
    private static readonly TimeSpan Step = TimeSpan.FromMicroseconds(1);

    private readonly TDbContext _dbContext;
    private readonly IntegrationEventRegistry _registry;
    private readonly IMessageContext _messageContext;
    private DateTimeOffset? _lastOccurredAt;

    public OutboxWriter(TDbContext dbContext, IntegrationEventRegistry registry, IMessageContext messageContext)
    {
        _dbContext = dbContext;
        _registry = registry;
        _messageContext = messageContext;
    }

    public void Enqueue(IntegrationEvent integrationEvent)
    {
        if (_lastOccurredAt is { } last && integrationEvent.OccurredAt <= last)
        {
            integrationEvent = integrationEvent with { OccurredAt = last + Step };
        }

        _lastOccurredAt = integrationEvent.OccurredAt;

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
