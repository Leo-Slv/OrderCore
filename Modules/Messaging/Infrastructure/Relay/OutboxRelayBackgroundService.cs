using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Relay;

/// <summary>
/// Publishes what the modules wrote to their outboxes (section 20). Every
/// poll, for each registered outbox, it takes the oldest unsent rows and
/// publishes them in order on a channel with publisher confirmations; a row
/// is marked sent only after the broker confirmed it, so a crash in between
/// publishes it again later — delivery is at least once and consumers are
/// idempotent. A failed publish stops that outbox's batch (keeping the
/// order), is recorded on the row and retried on the next poll; nothing
/// that goes wrong here stops the service or the API.
/// <para>
/// One API instance is assumed (Tracking spec, decision 4): with several,
/// the rows would need claiming (<c>FOR UPDATE SKIP LOCKED</c>) to avoid
/// publishing twice.
/// </para>
/// </summary>
public sealed class OutboxRelayBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnection _connection;
    private readonly IntegrationEventRegistry _registry;
    private readonly RabbitMqOptions _options;
    private readonly MessagingOptions _messaging;
    private readonly TimeProvider _timeProvider;
    private readonly MessagingTelemetry _telemetry;
    private readonly ILogger<OutboxRelayBackgroundService> _logger;
    private IChannel? _channel;

    public OutboxRelayBackgroundService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnection connection,
        IntegrationEventRegistry registry,
        IOptions<RabbitMqOptions> options,
        IOptions<MessagingOptions> messaging,
        TimeProvider timeProvider,
        MessagingTelemetry telemetry,
        ILogger<OutboxRelayBackgroundService> logger)
    {
        _telemetry = telemetry;
        _scopeFactory = scopeFactory;
        _connection = connection;
        _registry = registry;
        _options = options.Value;
        _messaging = messaging.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_messaging.RelayPollInterval);

        do
        {
            foreach (var source in _registry.OutboxSources)
            {
                try
                {
                    await RelayAsync(source, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Relaying the {Outbox} outbox failed; retrying on the next poll.", source.Name);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
    }

    private async Task RelayAsync(Type source, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(source);
        var outbox = dbContext.Set<OutboxMessage>();

        var module = ModuleOf(dbContext);
        await ReportBacklogAsync(outbox, module, cancellationToken);

        var pending = await outbox
            .Where(m => m.SentAt == null)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.Id)
            .Take(_messaging.RelayBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                await PublishAsync(message, cancellationToken);
                message.SentAt = _timeProvider.GetUtcNow();
                message.LastError = null;
                _telemetry.Published(module, message.Type);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                message.PublishAttempts++;
                message.LastError = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;
                _telemetry.PublishFailed(module, message.Type);
                await dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    exception, "Publishing {Type} {MessageId} failed (attempt {Attempt}); retrying on the next poll.",
                    message.Type, message.Id, message.PublishAttempts);
                await ResetChannelAsync();
                return;
            }

            // Saved one by one: what the broker confirmed is never published again.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>The module an outbox belongs to, from its table name (<c>{module}_outbox_messages</c>).</summary>
    private static string ModuleOf(DbContext dbContext)
    {
        var table = dbContext.Model.FindEntityType(typeof(OutboxMessage))?.GetTableName() ?? dbContext.GetType().Name;
        return table.Split("_outbox_messages")[0];
    }

    /// <summary>How many rows wait and how old the oldest is — what the backlog gauges report.</summary>
    private async Task ReportBacklogAsync(DbSet<OutboxMessage> outbox, string module, CancellationToken cancellationToken)
    {
        var unsent = outbox.Where(m => m.SentAt == null);
        var pending = await unsent.LongCountAsync(cancellationToken);
        var oldest = pending == 0 ? (DateTimeOffset?)null : await unsent.MinAsync(m => (DateTimeOffset?)m.OccurredAt, cancellationToken);
        var age = oldest is { } occurredAt ? _timeProvider.GetUtcNow() - occurredAt : TimeSpan.Zero;
        _telemetry.ReportBacklog(module, pending, age < TimeSpan.Zero ? TimeSpan.Zero : age);
    }

    private async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (_channel is not { IsOpen: true })
        {
            await ResetChannelAsync();
            _channel = await _connection.CreatePublishingChannelAsync(cancellationToken);
        }

        var routingKey = IntegrationEventRegistry.RoutingKey(message.Type, message.Version);
        using var activity = _telemetry.StartPublish(_options.Exchange, routingKey, message.Id, message.TraceParent, message.TraceState);

        // The consumer continues the producer span (or, with no listener, the
        // trace of whoever wrote the row).
        var traceParent = activity?.Id ?? message.TraceParent;
        var traceState = activity is null ? message.TraceState : activity.TraceStateString;
        var headers = new Dictionary<string, object?> { [MessageHeaders.Attempt] = 1 };
        if (traceParent is not null)
        {
            headers[MessageHeaders.TraceParent] = Encoding.UTF8.GetBytes(traceParent);
        }

        if (traceState is not null)
        {
            headers[MessageHeaders.TraceState] = Encoding.UTF8.GetBytes(traceState);
        }

        var properties = new BasicProperties
        {
            MessageId = message.Id.ToString(),
            Type = message.Type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            Headers = headers,
        };

        // A confirmation that never comes must not stall the relay: past the
        // timeout the publish counts as failed and is retried next poll.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_messaging.PublishTimeout);

        try
        {
            await _channel.BasicPublishAsync(
                _options.Exchange, routingKey, mandatory: false, properties, MessageEnvelope.FromOutbox(message).ToBytes(), timeout.Token);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            await _channel.DisposeAsync();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Disposing a broken relay channel failed.");
        }

        _channel = null;
    }
}
