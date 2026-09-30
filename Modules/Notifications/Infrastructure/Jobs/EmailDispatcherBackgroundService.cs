using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Notifications.Application.UseCases;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Section <c>Notifications:Dispatcher</c>.</summary>
public sealed class EmailDispatcherOptions
{
    public const string SectionName = "Notifications:Dispatcher";

    /// <summary>How often queued e-mails are looked for.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// The waits between attempts after a transient failure; empty = the
    /// default 30 s, 2 min, 10 min, 30 min (five attempts in all).
    /// </summary>
    public TimeSpan[] RetryDelays { get; set; } = [];

    /// <summary>Records of e-mails sent or given up on longer ago than this are removed.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(90);

    public TimeSpan RetentionInterval { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>
/// Sends the queued e-mails every <see cref="EmailDispatcherOptions.Interval"/>:
/// the due ids are listed in one scope and each message is sent in its own,
/// so one failing is recorded and retried without holding up the others (as
/// the payment jobs do). Also removes old records, once per
/// <see cref="EmailDispatcherOptions.RetentionInterval"/>. One API instance
/// is assumed, as for the outbox relay.
/// </summary>
public sealed class EmailDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailDispatcherOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailDispatcherBackgroundService> _logger;
    private DateTimeOffset? _lastPurge;

    public EmailDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailDispatcherOptions> options,
        TimeProvider timeProvider,
        ILogger<EmailDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SendDueAsync(stoppingToken);
                await PurgeIfDueAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Sending queued e-mails failed; retrying on the next check.");
            }
        }
    }

    /// <summary>One pass: every e-mail due, each on its own.</summary>
    public async Task SendDueAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> due;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<SendEmailUseCase>().FindDueAsync(_options.BatchSize, cancellationToken);
        }

        foreach (var messageId in due)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SendEmailUseCase>().SendAsync(messageId, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Sending e-mail {EmailMessageId} failed; retrying on the next check.", messageId);
            }
        }
    }

    private async Task PurgeIfDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_lastPurge is { } last && now - last < _options.RetentionInterval)
        {
            return;
        }

        _lastPurge = now;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var removed = await scope.ServiceProvider.GetRequiredService<PurgeFinishedEmailsUseCase>()
            .ExecuteAsync(_options.Retention, _options.BatchSize, cancellationToken);
        if (removed > 0)
        {
            _logger.LogInformation("Removed {Count} e-mail record(s) past their retention.", removed);
        }
    }
}
