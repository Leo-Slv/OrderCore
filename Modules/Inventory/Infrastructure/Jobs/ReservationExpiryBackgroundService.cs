using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.UseCases;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Jobs;

/// <summary>Section <c>Inventory:ReservationExpiry</c>.</summary>
public sealed class ReservationExpiryOptions
{
    public const string SectionName = "Inventory:ReservationExpiry";

    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(5);

    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// The safety net under the order and payment deadlines
/// (Docs/specs/orders/unpaid-order-expiry.md, decision 2): every
/// <see cref="ReservationExpiryOptions.CheckInterval"/>, reservations still
/// holding stock past their <c>ExpiresAt</c> (2 hours) are expired and their
/// units go back to available. Each one is logged as a warning — with its
/// order and product — because the deadlines upstream should always get there
/// first; one found here means something upstream broke. Each reservation in
/// its own scope; one instance assumed.
/// </summary>
public sealed class ReservationExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationExpiryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationExpiryBackgroundService> _logger;

    public ReservationExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationExpiryOptions> options,
        TimeProvider timeProvider,
        ILogger<ReservationExpiryBackgroundService> logger)
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
                _logger.LogError(exception, "Checking for expired reservations failed; retrying on the next check.");
            }
        }
    }

    /// <summary>One pass over the reservations past their time.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> expired;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<IInventoryReservationRepository>()
                .ListExpiredActiveAsync(_timeProvider.GetUtcNow(), _options.BatchSize, cancellationToken);
        }

        foreach (var reservationId in expired)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ExpireReservationUseCase>()
                    .ExecuteAsync(reservationId, cancellationToken);
                if (result is not null)
                {
                    _logger.LogWarning(
                        "Reservation {ReservationId} of order {OrderId} held {Quantity} unit(s) of product {ProductId} past its lifetime and was expired; the order and payment deadlines should have released it.",
                        result.ReservationId, result.OrderId, result.Quantity, result.ProductId);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Expiring reservation {ReservationId} failed; retrying on the next check.", reservationId);
            }
        }
    }
}
