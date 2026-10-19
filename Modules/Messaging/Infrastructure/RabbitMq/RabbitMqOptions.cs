namespace OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

/// <summary>
/// Where the broker is (configuration section <c>RabbitMq</c>). The password
/// comes from the environment or user-secrets, never from committed
/// settings — the API refuses to start without it, like the JWT key.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Connect over TLS (AMQPS, usually port 5671), verifying the broker's certificate for
    /// <see cref="Host"/>. Required for a managed broker reached over the internet
    /// (Docs/operations/vercel-hosting.md); off for the one in docker compose.
    /// </summary>
    public bool Tls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>The topic exchange every integration event is published to.</summary>
    public string Exchange { get; set; } = "ordercore.events";

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// How messages are retried and relayed. Not bound from configuration as a whole: the
/// retry schedule is a decision (five attempts — immediately, then after
/// 10 s, 1 min, 5 min and 30 min), and only the test host shortens it. The one
/// setting a deployment may change is <see cref="RelayPollInterval"/>
/// (<c>Messaging:RelayPollInterval</c>, e.g. <c>00:00:10</c>): the relay wakes the
/// database that often, which matters where compute is metered (Vercel's
/// Daemon CPU quota, a serverless database that sleeps when idle).
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>The wait before attempts 2, 3, 4 and 5.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)];

    public int MaxAttempts => RetryDelays.Count + 1;

    public const string RelayPollIntervalSetting = "Messaging:RelayPollInterval";

    /// <summary>How often the relay looks for unsent outbox rows; an event waits at most this long for the broker.</summary>
    public TimeSpan RelayPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Applies the settings a deployment may change (see the type's remarks); an invalid value stops the API.</summary>
    public void ApplyConfiguration(IConfiguration configuration)
    {
        var value = configuration[RelayPollIntervalSetting];
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!TimeSpan.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var interval)
            || interval < TimeSpan.FromMilliseconds(100)
            || interval > TimeSpan.FromMinutes(10))
        {
            throw new InvalidOperationException(
                $"{RelayPollIntervalSetting} must be a duration between 00:00:00.100 and 00:10:00 (it is '{value}').");
        }

        RelayPollInterval = interval;
    }

    public int RelayBatchSize { get; set; } = 100;

    /// <summary>
    /// How long a publish waits for the broker's confirmation before it
    /// counts as failed — so a confirmation that never comes (a channel the
    /// broker closed mid-publish) can't stall the relay.
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long the client waits between attempts to recover a lost connection.</summary>
    public TimeSpan ConnectionRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

    public ushort ConsumerPrefetch { get; set; } = 10;

    /// <summary>How long startup waits for the broker before the API refuses to start.</summary>
    public int StartupConnectAttempts { get; set; } = 10;

    public TimeSpan StartupConnectDelay { get; set; } = TimeSpan.FromSeconds(3);
}
