using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.UnitTests.Payments;

/// <summary>
/// A controllable <see cref="IPaymentProvider"/> test double, distinct from
/// the real <c>FakePaymentProvider</c> (Infrastructure) — this one is only
/// for unit tests exercising use case orchestration, not app behavior.
/// </summary>
internal sealed class StubPaymentProvider : IPaymentProvider
{
    private readonly bool _succeeds;

    public StubPaymentProvider(bool succeeds = true)
    {
        _succeeds = succeeds;
    }

    public Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(_succeeds
            ? new PaymentAuthorizationResult(true, $"stub_ref_{payment.Id:N}", null)
            : new PaymentAuthorizationResult(false, null, "stub_declined"));

    public Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(_succeeds ? new PaymentCaptureResult(true, null) : new PaymentCaptureResult(false, "stub_capture_failed"));

    public Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(_succeeds ? new PaymentRefundResult(true, null) : new PaymentRefundResult(false, "stub_refund_failed"));
}
