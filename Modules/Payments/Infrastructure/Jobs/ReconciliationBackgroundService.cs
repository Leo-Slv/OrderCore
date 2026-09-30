using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.UseCases;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Jobs;

/// <summary>
/// Every <see cref="ReconciliationOptions.Interval"/>, reconciles the payments
/// whose provider state may have moved without a webhook reaching OrderCore:
/// still <c>Processing</c> after <see cref="ReconciliationOptions.ProcessingAge"/>,
/// or <c>Authorized</c> past their authorization's expiry. Each payment in its
/// own scope, so one failure (the provider down) is logged and retried on the
/// next run without holding up the others. One API instance is assumed.
/// </summary>
public sealed class ReconciliationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReconciliationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReconciliationBackgroundService> _logger;

    public ReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReconciliationOptions> options,
        TimeProvider timeProvider,
        ILogger<ReconciliationBackgroundService> logger)
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
                await RunAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Reconciling payments failed; retrying on the next run.");
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> candidates;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var now = _timeProvider.GetUtcNow();
            candidates = await scope.ServiceProvider.GetRequiredService<IPaymentRepository>()
                .ListToReconcileAsync(now - _options.ProcessingAge, now, _options.BatchSize, cancellationToken);
        }

        foreach (var paymentId in candidates)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReconcilePaymentUseCase>().ExecuteAsync(paymentId, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Reconciling payment {PaymentId} failed; retrying on the next run.", paymentId);
            }
        }
    }
}
