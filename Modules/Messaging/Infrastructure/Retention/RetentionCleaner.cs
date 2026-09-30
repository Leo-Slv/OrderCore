using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;
using OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Retention;

/// <summary>Section <c>Messaging:Retention</c> (production-readiness spec, decision 6).</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Messaging:Retention";

    /// <summary>Sent outbox rows and handled inbox rows older than this are removed.</summary>
    public TimeSpan MessageRecords { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Failed messages replayed or discarded longer ago than this are removed; pending ones never are.</summary>
    public TimeSpan ResolvedFailedMessages { get; set; } = TimeSpan.FromDays(90);

    /// <summary>How often retention runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Rows removed per statement, so a first run on a large table doesn't hold long locks.</summary>
    public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// Removes technical records past their retention (production-readiness
/// spec, item 7): sent rows of every registered outbox, handled rows of every
/// inbox, and resolved failed messages. The order timeline and the audit log
/// are business history and are never touched. Deletes in batches; every
/// table is cleaned on its own, so one failing doesn't stop the others.
/// </summary>
public sealed class RetentionCleaner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IntegrationEventRegistry _registry;
    private readonly RetentionOptions _options;
    private readonly MessagingTelemetry _telemetry;
    private readonly ILogger<RetentionCleaner> _logger;

    public RetentionCleaner(
        IServiceScopeFactory scopeFactory,
        IntegrationEventRegistry registry,
        IOptions<RetentionOptions> options,
        MessagingTelemetry telemetry,
        ILogger<RetentionCleaner> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _options = options.Value;
        _telemetry = telemetry;
        _logger = logger;
    }

    /// <returns>How many rows were removed, in total.</returns>
    public async Task<long> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var messageCutoff = now - _options.MessageRecords;
        var failedCutoff = now - _options.ResolvedFailedMessages;
        long total = 0;

        foreach (var source in _registry.OutboxSources)
        {
            total += await CleanAsync<OutboxMessage>(
                source, rows => rows.Where(m => m.SentAt != null && m.SentAt < messageCutoff).OrderBy(m => m.SentAt), cancellationToken);
        }

        foreach (var source in _registry.InboxSources)
        {
            total += await CleanAsync<InboxMessage>(
                source, rows => rows.Where(m => m.ProcessedAt < messageCutoff).OrderBy(m => m.ProcessedAt), cancellationToken);
        }

        total += await CleanAsync<FailedMessagePersistenceModel>(
            typeof(MessagingDbContext),
            rows => rows.Where(m => m.ResolvedAt != null && m.ResolvedAt < failedCutoff).OrderBy(m => m.ResolvedAt),
            cancellationToken);

        return total;
    }

    private async Task<long> CleanAsync<TRow>(
        Type dbContextType, Func<IQueryable<TRow>, IQueryable<TRow>> expired, CancellationToken cancellationToken)
        where TRow : class
    {
        string? table = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);
            table = dbContext.Model.FindEntityType(typeof(TRow))?.GetTableName() ?? typeof(TRow).Name;

            long removed = 0;
            int batch;
            do
            {
                batch = await expired(dbContext.Set<TRow>()).Take(_options.BatchSize).ExecuteDeleteAsync(cancellationToken);
                removed += batch;
            }
            while (batch == _options.BatchSize);

            if (removed > 0)
            {
                _telemetry.RetentionDeleted(table, removed);
                _logger.LogInformation("Retention removed {Rows} row(s) from {Table}.", removed, table);
            }

            return removed;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Retention could not clean {Table}; it is tried again on the next run.", table ?? dbContextType.Name);
            return 0;
        }
    }
}

/// <summary>Runs <see cref="RetentionCleaner"/> every <see cref="RetentionOptions.Interval"/> (daily).</summary>
public sealed class RetentionBackgroundService : BackgroundService
{
    private readonly RetentionCleaner _cleaner;
    private readonly RetentionOptions _options;
    private readonly TimeProvider _timeProvider;

    public RetentionBackgroundService(RetentionCleaner cleaner, IOptions<RetentionOptions> options, TimeProvider timeProvider)
    {
        _cleaner = cleaner;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await _cleaner.RunAsync(_timeProvider.GetUtcNow(), stoppingToken);
        }
    }
}
