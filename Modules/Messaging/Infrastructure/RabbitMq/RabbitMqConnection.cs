using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// The API's one connection to RabbitMQ (connections are expensive; channels
/// are cheap and opened per use). Opened by <see cref="RabbitMqStartupService"/>
/// before anything else runs; the client recovers it automatically after a
/// network failure, and callers reopen their channels when they find them
/// closed.
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } open)
        {
            return open;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true } opened)
            {
                return opened;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.Username,
                Password = _options.Password,
                ClientProvidedName = "ordercore-api",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>A channel whose publishes wait for the broker's confirmation (and throw when it refuses).</summary>
    public async Task<IChannel> CreatePublishingChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await GetAsync(cancellationToken);
        return await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}
