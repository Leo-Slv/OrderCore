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

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>The topic exchange every integration event is published to.</summary>
    public string Exchange { get; set; } = "ordercore.events";

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

/// <summary>
/// How messages are retried and relayed. Not bound from configuration: the
/// retry schedule is a decision (five attempts — immediately, then after
/// 10 s, 1 min, 5 min and 30 min), and only the test host shortens it.
/// </summary>
public sealed class MessagingOptions
{
    /// <summary>The wait before attempts 2, 3, 4 and 5.</summary>
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } =
        [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)];

    public int MaxAttempts => RetryDelays.Count + 1;

    public TimeSpan RelayPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int RelayBatchSize { get; set; } = 100;

    /// <summary>
    /// How long a publish waits for the broker's confirmation before it
    /// counts as failed — so a confirmation that never comes (a channel the
    /// broker closed mid-publish) can't stall the relay.
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public ushort ConsumerPrefetch { get; set; } = 10;

    /// <summary>How long startup waits for the broker before the API refuses to start.</summary>
    public int StartupConnectAttempts { get; set; } = 10;

    public TimeSpan StartupConnectDelay { get; set; } = TimeSpan.FromSeconds(3);
}
