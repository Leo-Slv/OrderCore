using System.Diagnostics;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Providers;

/// <summary>
/// Wraps the configured <see cref="IPaymentProvider"/> and records how long
/// each call took and how it ended (<see cref="PaymentsMetrics.ProviderCalled"/>):
/// <c>succeeded</c>, <c>refused</c> (the provider answered no) or
/// <c>error</c> (the call itself failed). Any provider gets this for free,
/// the fake one today and Stripe next.
/// </summary>
public sealed class MeasuredPaymentProvider : IPaymentProvider
{
    private readonly IPaymentProvider _inner;
    private readonly PaymentsMetrics _metrics;
    public MeasuredPaymentProvider(IPaymentProvider inner, PaymentsMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
    }

    public PaymentProviderInfo Info => _inner.Info;

    public Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken) =>
        MeasureAsync("authorize", () => _inner.AuthorizeAsync(payment, cancellationToken), r => r.Succeeded);

    public Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken) =>
        MeasureAsync("capture", () => _inner.CaptureAsync(payment, cancellationToken), r => r.Succeeded);

    public Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken) =>
        MeasureAsync("refund", () => _inner.RefundAsync(payment, cancellationToken), r => r.Succeeded);

    public Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken) =>
        MeasureAsync("void", () => _inner.VoidAsync(payment, cancellationToken), r => r.Succeeded);

    private async Task<TResult> MeasureAsync<TResult>(string operation, Func<Task<TResult>> call, Func<TResult, bool> succeeded)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await call();
            _metrics.ProviderCalled(_inner.Info.Name, operation, succeeded(result) ? "succeeded" : "refused", Stopwatch.GetElapsedTime(started));
            return result;
        }
        catch
        {
            _metrics.ProviderCalled(_inner.Info.Name, operation, "error", Stopwatch.GetElapsedTime(started));
            throw;
        }
    }
}
