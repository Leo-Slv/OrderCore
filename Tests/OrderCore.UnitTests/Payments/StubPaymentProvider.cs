using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// A controllable <see cref="IPaymentProvider"/> test double, distinct from
/// the real <c>FakePaymentProvider</c> (Infrastructure) — this one is only
/// for unit tests exercising use case orchestration, not app behavior.
/// Counts the calls, so a test can tell an idempotent no-op from a second
/// trip to the provider.
/// </summary>
internal sealed class StubPaymentProvider : IPaymentProvider
{
    private readonly bool _succeeds;

    public StubPaymentProvider(bool succeeds = true)
    {
        _succeeds = succeeds;
    }

    public int CaptureCalls { get; private set; }

    public int VoidCalls { get; private set; }

    public int RefundCalls { get; private set; }

    public Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(_succeeds
            ? new PaymentAuthorizationResult(true, $"stub_ref_{payment.Id:N}", null)
            : new PaymentAuthorizationResult(false, null, "stub_declined"));

    public Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken)
    {
        CaptureCalls++;
        return Task.FromResult(_succeeds ? new PaymentCaptureResult(true, null) : new PaymentCaptureResult(false, "stub_capture_failed"));
    }

    public Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken)
    {
        RefundCalls++;
        return Task.FromResult(_succeeds ? new PaymentRefundResult(true, null) : new PaymentRefundResult(false, "stub_refund_failed"));
    }

    public Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken)
    {
        VoidCalls++;
        return Task.FromResult(_succeeds ? new PaymentVoidResult(true, null) : new PaymentVoidResult(false, "stub_void_failed"));
    }
}
