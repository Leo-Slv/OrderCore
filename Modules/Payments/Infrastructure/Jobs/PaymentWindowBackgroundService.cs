using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Application.UseCases;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Jobs;

/// <summary>
/// Runs <see cref="ExpirePaymentWindowUseCase"/> every
/// <see cref="PaymentWindowOptions.CheckInterval"/>. Each payment is expired in
/// its own scope, so one that fails (the provider down, a concurrent change)
/// is logged and retried on the next check without holding up the others;
/// nothing here stops the service or the API. One API instance is assumed,
/// as for the outbox relay.
/// </summary>
public sealed class PaymentWindowBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PaymentWindowOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentWindowBackgroundService> _logger;

    public PaymentWindowBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PaymentWindowOptions> options,
        TimeProvider timeProvider,
        ILogger<PaymentWindowBackgroundService> logger)
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
                _logger.LogError(exception, "Checking the payment window failed; retrying on the next check.");
            }
        }
    }

    /// <summary>One pass: every payment past its window, each on its own.</summary>
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> expired;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            expired = await scope.ServiceProvider.GetRequiredService<ExpirePaymentWindowUseCase>()
                .FindExpiredAsync(_options.Window, _options.BatchSize, cancellationToken);
        }

        foreach (var paymentId in expired)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ExpirePaymentWindowUseCase>()
                    .ExpireAsync(paymentId, _options.Window, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Expiring payment {PaymentId} past its window failed; retrying on the next check.", paymentId);
            }
        }
    }
}
