using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Consumers;

public enum DeliveryOutcome
{
    Handled,

    /// <summary>This consumer already handled this message id; nothing was done.</summary>
    Duplicate,
}

/// <summary>
/// Handles one delivery for one consumer, broker-agnostic (the host deals
/// with acknowledgements and retries):
/// <list type="number">
/// <item>continues the trace the message carries, so everything the handler
/// does — including events it publishes — stays in the originating trace;</item>
/// <item>opens a scope and marks the message as the causation of anything
/// published in it;</item>
/// <item>skips a message id this consumer already handled (the inbox);</item>
/// <item>adds the inbox row to the consuming module's context and invokes
/// the handler — the row commits with the handler's first save (or right
/// after, if the handler saved nothing), and its key turns a concurrent
/// second delivery into a duplicate instead of a double effect.</item>
/// </list>
/// Exceptions from the handler propagate: the host decides between a retry
/// and the failed-message list.
/// </summary>
public sealed class MessageProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IntegrationEventRegistry _registry;
    private readonly TimeProvider _timeProvider;

    public MessageProcessor(IServiceScopeFactory scopeFactory, IntegrationEventRegistry registry, TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _timeProvider = timeProvider;
    }

    public async Task<DeliveryOutcome> ProcessAsync(
        string queue, MessageEnvelope envelope, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        var registration = _registry.Consumers.FirstOrDefault(c =>
                c.Queue == queue && _registry.ContractOf(c.EventType) == new EventContract(envelope.Type, envelope.Version))
            ?? throw new UnroutableMessageException(
                $"No handler on '{queue}' for '{IntegrationEventRegistry.RoutingKey(envelope.Type, envelope.Version)}'.");

        using var activity = ContinueTrace(traceParent, traceState);

        await using var scope = _scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<MessageContext>().BeginHandling(envelope.MessageId);

        var inbox = registration.InboxOf(scope.ServiceProvider);
        var alreadyHandled = await inbox.Set<InboxMessage>()
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == envelope.MessageId && m.Consumer == queue, cancellationToken);
        if (alreadyHandled)
        {
            return DeliveryOutcome.Duplicate;
        }

        inbox.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = envelope.MessageId,
            Consumer = queue,
            ProcessedAt = _timeProvider.GetUtcNow(),
        });

        try
        {
            await registration.Invoke(scope.ServiceProvider, envelope.ToEvent(registration.EventType), cancellationToken);
            await inbox.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsInboxConflict(exception))
        {
            return DeliveryOutcome.Duplicate;
        }

        return DeliveryOutcome.Handled;
    }

    /// <summary>
    /// Makes the message's trace the current one. A plain <see cref="Activity"/>
    /// (not from an <c>ActivitySource</c>) so the context flows even with no
    /// tracing listener; the Observability feature adds real consumer spans.
    /// </summary>
    private static Activity? ContinueTrace(string? traceParent, string? traceState)
    {
        if (traceParent is null || !ActivityContext.TryParse(traceParent, traceState, out _))
        {
            return null;
        }

        var activity = new Activity("OrderCore.Messaging.Consume");
        activity.SetParentId(traceParent);
        activity.TraceStateString = traceState;
        return activity.Start();
    }

    private static bool IsInboxConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
        && exception.Entries.Any(e => e.Entity is InboxMessage);
}

/// <summary>A message no handler on its queue can read (unknown type or version); retrying can't help.</summary>
public sealed class UnroutableMessageException : Exception
{
    public UnroutableMessageException(string message) : base(message)
    {
    }
}
