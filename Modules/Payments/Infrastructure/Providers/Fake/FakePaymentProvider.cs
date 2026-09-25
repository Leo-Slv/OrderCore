using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;

/// <summary>
/// Local/test double for a real payment provider (section 14). It exists
/// so that Orders/Payments flows, including failure and resilience paths,
/// can be developed and tested end-to-end before any real provider (e.g.
/// Stripe) is wired in.
/// </summary>
public sealed class FakePaymentProviderOptions
{
    /// <summary>
    /// Deterministic behavior switch used by tests. In "Success" mode every
    /// call succeeds; the other modes simulate the failure classes listed
    /// in section 43 of the project context.
    /// </summary>
    public FakePaymentProviderMode Mode { get; set; } = FakePaymentProviderMode.Success;

    public TimeSpan SimulatedLatency { get; set; } = TimeSpan.Zero;
}

public enum FakePaymentProviderMode
{
    Success,
    Declined,

    /// <summary>
    /// Authorizes, but refuses to capture: an order is confirmed and then
    /// can't be shipped. Void and refund still succeed.
    /// </summary>
    CaptureDeclined,
    Timeout,
    Unavailable,
}

public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly FakePaymentProviderOptions _options;

    public FakePaymentProvider(IOptions<FakePaymentProviderOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return _options.Mode switch
        {
            FakePaymentProviderMode.Success or FakePaymentProviderMode.CaptureDeclined => new PaymentAuthorizationResult(
                Succeeded: true,
                ProviderReference: $"fake_auth_{payment.Id:N}",
                FailureReason: null),
            FakePaymentProviderMode.Declined => new PaymentAuthorizationResult(
                Succeeded: false,
                ProviderReference: null,
                FailureReason: "card_declined"),
            FakePaymentProviderMode.Timeout => throw new TimeoutException("Simulated provider timeout."),
            FakePaymentProviderMode.Unavailable => throw new InvalidOperationException("Simulated provider unavailable."),
            _ => throw new NotSupportedException($"Unsupported mode '{_options.Mode}'."),
        };
    }

    public async Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return _options.Mode == FakePaymentProviderMode.Success
            ? new PaymentCaptureResult(Succeeded: true, FailureReason: null)
            : new PaymentCaptureResult(Succeeded: false, FailureReason: "capture_failed");
    }

    public async Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return AcceptsReversals
            ? new PaymentRefundResult(Succeeded: true, FailureReason: null)
            : new PaymentRefundResult(Succeeded: false, FailureReason: "refund_failed");
    }

    public async Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken)
    {
        await SimulateLatencyAsync(cancellationToken);

        return AcceptsReversals
            ? new PaymentVoidResult(Succeeded: true, FailureReason: null)
            : new PaymentVoidResult(Succeeded: false, FailureReason: "void_failed");
    }

    private bool AcceptsReversals => _options.Mode is FakePaymentProviderMode.Success or FakePaymentProviderMode.CaptureDeclined;

    private Task SimulateLatencyAsync(CancellationToken cancellationToken) =>
        _options.SimulatedLatency > TimeSpan.Zero
            ? Task.Delay(_options.SimulatedLatency, cancellationToken)
            : Task.CompletedTask;
}
