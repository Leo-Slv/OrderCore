using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Domain.Enums;

namespace OrderCore.Api.Modules.Payments.Domain.Repositories;

public sealed record PaymentAuthorizationResult(bool Succeeded, string? ProviderReference, string? FailureReason);

public sealed record PaymentCaptureResult(bool Succeeded, string? FailureReason);

public sealed record PaymentRefundResult(bool Succeeded, string? FailureReason);

public sealed record PaymentVoidResult(bool Succeeded, string? FailureReason);

/// <summary>
/// What a provider offers the storefront: its name, the payment methods it
/// takes, and — for providers whose card form runs in the browser (Stripe) —
/// the publishable key that form needs. Never a secret.
/// </summary>
public sealed record PaymentProviderInfo(string Name, IReadOnlyCollection<PaymentMethod> SupportedMethods, string? PublishableKey);

/// <summary>
/// Abstraction the Payments module depends on instead of a concrete
/// provider SDK (section 14 and section 38 — this abstraction exists
/// because multiple real implementations are expected: a
/// <c>FakePaymentProvider</c> for local development/tests, and eventually
/// <c>StripePaymentProvider</c>, both living in
/// Modules/Payments/Infrastructure/Providers).
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Which provider this is and what it can take (Docs/specs/payments/stripe-provider.md, decision 7).</summary>
    PaymentProviderInfo Info { get; }

    Task<PaymentAuthorizationResult> AuthorizeAsync(Payment payment, CancellationToken cancellationToken);

    Task<PaymentCaptureResult> CaptureAsync(Payment payment, CancellationToken cancellationToken);

    Task<PaymentRefundResult> RefundAsync(Payment payment, CancellationToken cancellationToken);

    /// <summary>Releases an authorization that was never captured.</summary>
    Task<PaymentVoidResult> VoidAsync(Payment payment, CancellationToken cancellationToken);
}
