using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;
using OrderCore.Api.Modules.Payments.Domain.Repositories;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// Answers every authorization the way Stripe does — waiting for the buyer,
/// with the PaymentIntent <see cref="IntentOf"/> — without a Stripe account,
/// so the host's checkout, cancellation and webhooks can be driven end to end.
/// </summary>
internal sealed class WaitingForBuyerProvider : IPaymentProvider
{
    public const string Secret = "pi_test_secret";

    private int _authorizations;
    private int _cancellations;

    public int Authorizations => _authorizations;

    public int Cancellations => _cancellations;

    public PaymentProviderInfo Info { get; } = new("Waiting", [PaymentMethod.Card, PaymentMethod.Pix], "pk_test");

    public static string IntentOf(Payment payment) => $"pi_{payment.Id:N}";

    public Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _authorizations);
        return Task.FromResult(PaymentAuthorizationResult.WaitingForBuyer(IntentOf(payment), Secret));
    }

    public Task<string?> GetClientSecretAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(payment.IsAwaitingBuyer ? Secret : null);

    public Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _cancellations);
        return Task.FromResult(new PaymentVoidResult(true, null));
    }

    public Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentCaptureResult(true, null));

    public Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentRefundResult(true, null));
}
