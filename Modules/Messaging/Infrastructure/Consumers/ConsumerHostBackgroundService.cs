using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Consumers;

/// <summary>
/// Consumes every registered queue with manual acknowledgements and hands
/// each delivery to <see cref="MessageProcessor"/>. Every delivery ends
/// acknowledged, one way or another, so no message ever blocks its queue:
/// <list type="bullet">
/// <item>handled, or a duplicate → acknowledged;</item>
/// <item>the handler threw → a copy goes to the waiting queue of that
/// attempt (<see cref="RabbitMqTopology.RetryQueue"/>) and comes back after
/// its delay, up to the fifth attempt; then it is recorded as a
/// <see cref="FailedMessage"/> for the backoffice;</item>
/// <item>unreadable (not an envelope, unknown type) → recorded as failed at
/// once, since retrying can't help.</item>
/// </list>
/// Only if even that bookkeeping fails is the delivery handed back to the
/// broker (requeued), so nothing is lost. Nothing that happens while
/// handling a message stops the service.
/// </summary>
public sealed class ConsumerHostBackgroundService : BackgroundService
{
    private readonly RabbitMqConnection _connection;
    private readonly IntegrationEventRegistry _registry;
    private readonly MessageProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagingOptions _messaging;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsumerHostBackgroundService> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly List<IChannel> _channels = new();
    private IChannel? _publishChannel;

    public ConsumerHostBackgroundService(
        RabbitMqConnection connection,
        IntegrationEventRegistry registry,
        MessageProcessor processor,
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> messaging,
        TimeProvider timeProvider,
        ILogger<ConsumerHostBackgroundService> logger)
    {
        _connection = connection;
        _registry = registry;
        _processor = processor;
        _scopeFactory = scopeFactory;
        _messaging = messaging.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _connection.GetAsync(stoppingToken);

        foreach (var queue in _registry.Consumers.Select(c => c.Queue).Distinct())
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await channel.BasicQosAsync(0, _messaging.ConsumerPrefetch, global: false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(queue, channel, delivery, stoppingToken);
            await channel.BasicConsumeAsync(queue, autoAck: false, consumer, stoppingToken);
            _channels.Add(channel);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Stopping: unacknowledged deliveries go back to their queues when the channels close.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var channel in _channels)
        {
            await channel.DisposeAsync();
        }

        if (_publishChannel is not null)
        {
            await _publishChannel.DisposeAsync();
        }
    }

    private async Task OnReceivedAsync(string queue, IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var headers = delivery.BasicProperties.Headers;
        var attempt = ReadInt(headers, MessageHeaders.Attempt) ?? 1;
        var body = delivery.Body.ToArray();
        MessageEnvelope? envelope = null;

        try
        {
            try
            {
                envelope = MessageEnvelope.FromBytes(body);
                await _processor.ProcessAsync(
                    queue, envelope, ReadString(headers, MessageHeaders.TraceParent), ReadString(headers, MessageHeaders.TraceState), stoppingToken);
            }
            catch (Exception exception) when (exception is UnroutableMessageException or JsonException)
            {
                _logger.LogError(exception, "Message on {Queue} can't be handled; recorded as failed without retrying.", queue);
                await RecordFailedAsync(queue, envelope, body, headers, attempt, exception, stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                if (attempt < _messaging.MaxAttempts)
                {
                    _logger.LogWarning(
                        exception, "Handling {Type} {MessageId} on {Queue} failed (attempt {Attempt}); retrying in {Delay}.",
                        envelope?.Type, envelope?.MessageId, queue, attempt, _messaging.RetryDelays[attempt - 1]);
                    await ScheduleRetryAsync(queue, delivery, attempt, exception, stoppingToken);
                }
                else
                {
                    _logger.LogError(
                        exception, "Handling {Type} {MessageId} on {Queue} failed {Attempts} times; recorded as failed.",
                        envelope?.Type, envelope?.MessageId, queue, attempt);
                    await RecordFailedAsync(queue, envelope, body, headers, attempt, exception, stoppingToken);
                }
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping mid-message: left unacknowledged, the broker redelivers it.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Couldn't settle a delivery on {Queue}; handing it back to the broker.", queue);
            await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            try
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            }
            catch (Exception nackException)
            {
                _logger.LogError(nackException, "Couldn't hand a delivery on {Queue} back; the broker redelivers it when the channel closes.", queue);
            }
        }
    }

    private async Task ScheduleRetryAsync(
        string queue, BasicDeliverEventArgs delivery, int attempt, Exception exception, CancellationToken cancellationToken)
    {
        var original = delivery.BasicProperties.Headers;
        var headers = original is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(original);
        headers[MessageHeaders.Attempt] = attempt + 1;
        headers[MessageHeaders.LastError] = Encoding.UTF8.GetBytes(Truncate(exception.Message, 1000));
        headers.TryAdd(MessageHeaders.FirstFailedAt, Encoding.UTF8.GetBytes(_timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)));

        var properties = new BasicProperties
        {
            MessageId = delivery.BasicProperties.MessageId,
            Type = delivery.BasicProperties.Type,
            ContentType = delivery.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = delivery.BasicProperties.Timestamp,
            Headers = headers,
        };

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is not { IsOpen: true })
            {
                _publishChannel = await _connection.CreatePublishingChannelAsync(cancellationToken);
            }

            // Straight to the waiting queue (default exchange); its TTL sends it back to `queue`.
            await _publishChannel.BasicPublishAsync(
                string.Empty, RabbitMqTopology.RetryQueue(queue, attempt), mandatory: false, properties, delivery.Body, cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task RecordFailedAsync(
        string queue,
        MessageEnvelope? envelope,
        byte[] body,
        IDictionary<string, object?>? headers,
        int attempts,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var firstFailedAt = DateTimeOffset.TryParse(
            ReadString(headers, MessageHeaders.FirstFailedAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var first)
            ? first
            : now;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFailedMessageRepository>();
        await repository.AddAsync(
            FailedMessage.Record(
                envelope?.MessageId ?? Guid.Empty,
                envelope?.Type ?? string.Empty,
                envelope?.Version ?? 0,
                queue,
                Encoding.UTF8.GetString(body),
                ReadString(headers, MessageHeaders.TraceParent),
                ReadString(headers, MessageHeaders.TraceState),
                $"{exception.GetType().Name}: {exception.Message}",
                attempts,
                firstFailedAt,
                now),
            cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string? ReadString(IDictionary<string, object?>? headers, string name) =>
        headers is not null && headers.TryGetValue(name, out var value)
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => value?.ToString(),
            }
            : null;

    private static int? ReadInt(IDictionary<string, object?>? headers, string name) =>
        headers is not null && headers.TryGetValue(name, out var value)
            ? value switch
            {
                int number => number,
                long number => (int)number,
                byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
                _ => null,
            }
            : null;

    private static string Truncate(string text, int length) => text.Length > length ? text[..length] : text;
}
