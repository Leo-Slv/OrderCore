using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;
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
/// <item>handles it inside a consumer span continuing the trace the message
/// carries (<see cref="MessagingTelemetry.StartProcess"/>), so everything the
/// handler does — including events it publishes — stays in the originating
/// trace, and records how long it took and how it ended;</item>
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
    private readonly MessagingTelemetry _telemetry;

    public MessageProcessor(
        IServiceScopeFactory scopeFactory, IntegrationEventRegistry registry, TimeProvider timeProvider, MessagingTelemetry telemetry)
    {
        _telemetry = telemetry;
        _scopeFactory = scopeFactory;
        _registry = registry;
        _timeProvider = timeProvider;
    }

    public async Task<DeliveryOutcome> ProcessAsync(
        string queue, MessageEnvelope envelope, int attempt, string? traceParent, string? traceState, CancellationToken cancellationToken)
    {
        var registration = _registry.Consumers.FirstOrDefault(c =>
                c.Queue == queue && _registry.ContractOf(c.EventType) == new EventContract(envelope.Type, envelope.Version))
            ?? throw new UnroutableMessageException(
                $"No handler on '{queue}' for '{IntegrationEventRegistry.RoutingKey(envelope.Type, envelope.Version)}'.");

        using var activity = _telemetry.StartProcess(queue, envelope.Type, envelope.MessageId, attempt, traceParent, traceState);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var outcome = await HandleAsync(registration, queue, envelope, cancellationToken);
            _telemetry.Handled(queue, envelope.Type, outcome == DeliveryOutcome.Handled ? "handled" : "duplicate", Stopwatch.GetElapsedTime(started));
            return outcome;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            _telemetry.Handled(queue, envelope.Type, "error", Stopwatch.GetElapsedTime(started));
            throw;
        }
    }

    private async Task<DeliveryOutcome> HandleAsync(
        ConsumerRegistration registration, string queue, MessageEnvelope envelope, CancellationToken cancellationToken)
    {
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
