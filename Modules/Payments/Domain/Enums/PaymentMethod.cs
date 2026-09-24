namespace OrderCore.Api.Modules.Payments.Domain.Enums;

/// <summary>
/// How the buyer chose to pay. This is not the same as <c>Payment.Provider</c>,
/// which is who processes the payment: both methods currently go through
/// the same provider (<c>FakePaymentProvider</c>). See
/// Docs/specs/storefront/storefront-api-mvp.md, resolved decision 1.
/// </summary>
public enum PaymentMethod
{
    Card,
    Pix,
}
