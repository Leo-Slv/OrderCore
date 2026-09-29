namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>What the provider says happened to one of its payments, in OrderCore's terms.</summary>
public enum PaymentProviderUpdateKind
{
    /// <summary>The buyer confirmed the card and the amount is held.</summary>
    Authorized,

    /// <summary>The buyer's attempt was declined; they may try again.</summary>
    Declined,

    Captured,

    /// <summary>The provider cancelled the payment (see <see cref="PaymentProviderUpdate.AuthorizationExpired"/>).</summary>
    Canceled,

    RefundSucceeded,
    RefundFailed,
    DisputeOpened,
}

/// <summary>
/// One thing the provider told OrderCore about a payment — from a webhook
/// today, from reconciliation later — so both go through the same
/// transitions. Provider-neutral: the Stripe specifics stay in
/// Infrastructure.
/// </summary>
/// <param name="ProviderReference">The provider's id of the payment (Stripe: the PaymentIntent).</param>
/// <param name="Reason">A decline, refund failure or dispute reason code, when there is one.</param>
/// <param name="RefundId">OrderCore's refund, for the refund updates.</param>
/// <param name="AuthorizationExpired">For <see cref="PaymentProviderUpdateKind.Canceled"/>: the provider cancelled it because the authorization expired.</param>
/// <param name="AuthorizationExpiresAt">For <see cref="PaymentProviderUpdateKind.Authorized"/>: the provider's capture deadline, when known.</param>
public sealed record PaymentProviderUpdate(
    PaymentProviderUpdateKind Kind,
    string ProviderReference,
    DateTimeOffset OccurredAt,
    string? Reason = null,
    Guid? RefundId = null,
    bool AuthorizationExpired = false,
    DateTimeOffset? AuthorizationExpiresAt = null);

public enum PaymentProviderUpdateOutcome
{
    /// <summary>The payment changed.</summary>
    Applied,

    /// <summary>Already so, or the payment has moved on (a late or repeated update).</summary>
    Ignored,

    /// <summary>No OrderCore payment has that provider reference.</summary>
    UnknownPayment,
}
