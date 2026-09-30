using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Orders.Application.UseCases;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Jobs;

/// <summary>Section <c>Orders:UnpaidOrderExpiry</c> (unpaid-order spec, decision 1).</summary>
public sealed class UnpaidOrderExpiryOptions
{
    public const string SectionName = "Orders:UnpaidOrderExpiry";

    /// <summary>How long an order may wait without any payment started.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Runs <see cref="ExpireUnpaidOrderUseCase"/> every
/// <see cref="UnpaidOrderExpiryOptions.CheckInterval"/>: ids listed in one
/// scope, each order ended in its own, a failure logged and retried on the
/// next check (the payment jobs' shape). One API instance is assumed.
/// </summary>
public sealed class UnpaidOrderExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly UnpaidOrderExpiryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnpaidOrderExpiryBackgroundService> _logger;

    public UnpaidOrderExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<UnpaidOrderExpiryOptions> options,
        TimeProvider timeProvider,
        ILogger<UnpaidOrderExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CheckInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Checking for unpaid orders failed; retrying on the next check.");
            }
        }
    }

    private async Task CheckAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> expired;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<ExpireUnpaidOrderUseCase>()
                .FindExpiredAsync(_options.Window, _options.BatchSize, cancellationToken);
        }

        foreach (var orderId in expired)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ExpireUnpaidOrderUseCase>()
                    .ExpireAsync(orderId, _options.Window, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Ending unpaid order {OrderId} failed; retrying on the next check.", orderId);
            }
        }
    }
}
