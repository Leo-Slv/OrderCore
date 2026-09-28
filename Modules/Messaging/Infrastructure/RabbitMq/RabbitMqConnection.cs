using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// The API's one connection to RabbitMQ (connections are expensive; channels
/// are cheap and opened per use). Opened by <see cref="RabbitMqStartupService"/>
/// before anything else runs; the client recovers it automatically after a
/// network failure, and callers reopen their channels when they find them
/// closed.
/// <para>
/// Once opened, the connection is never replaced: while it is recovering it
/// is not open, and a caller asking for a channel then gets an error and
/// retries later. Replacing it would throw away the recovery — and with it
/// the consumers, which the client subscribes again only on the connection
/// it recovers.
/// </para>
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly MessagingOptions _messaging;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options, IOptions<MessagingOptions> messaging)
    {
        _options = options.Value;
        _messaging = messaging.Value;
    }

    /// <summary>
    /// The connection, opened on first use. It may be recovering (not open)
    /// after a network failure; channels asked for meanwhile fail, and the
    /// caller tries again later.
    /// </summary>
    public async Task<IConnection> GetAsync(CancellationToken cancellationToken)
    {
        if (_connection is { } existing)
        {
            return existing;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { } opened)
            {
                return opened;
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
                NetworkRecoveryInterval = _messaging.ConnectionRecoveryInterval,
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
