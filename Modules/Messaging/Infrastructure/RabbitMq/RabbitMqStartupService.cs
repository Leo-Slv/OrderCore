using Microsoft.Extensions.Options;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// Runs before the relay and the consumers: connects to RabbitMQ — retrying
/// for a while, since the broker may still be starting next to the API in
/// <c>docker compose</c> — and declares the topology. If the broker can't be
/// reached, the API refuses to start (Docs/specs/events/async-messaging.md,
/// decision 6: there is no in-process fallback).
/// </summary>
public sealed class RabbitMqStartupService : IHostedService
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly MessagingOptions _messaging;
    private readonly IntegrationEventRegistry _registry;
    private readonly ILogger<RabbitMqStartupService> _logger;

    public RabbitMqStartupService(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IOptions<MessagingOptions> messaging,
        IntegrationEventRegistry registry,
        ILogger<RabbitMqStartupService> logger)
    {
        _connection = connection;
        _options = options.Value;
        _messaging = messaging.Value;
        _registry = registry;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _connection.GetAsync(cancellationToken);
                break;
            }
            catch (Exception exception) when (attempt < _messaging.StartupConnectAttempts && exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "RabbitMQ at {Host}:{Port} isn't reachable yet (attempt {Attempt}); retrying.", _options.Host, _options.Port, attempt);
                await Task.Delay(_messaging.StartupConnectDelay, cancellationToken);
            }
        }

        await using var channel = await _connection.CreatePublishingChannelAsync(cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, _options, _messaging, _registry, cancellationToken);

        _logger.LogInformation(
            "Connected to RabbitMQ at {Host}:{Port}{VirtualHost}; {Queues} consumer queue(s) declared.",
            _options.Host,
            _options.Port,
            _options.VirtualHost,
            _registry.Consumers.Select(c => c.Queue).Distinct().Count());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
