namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

/// <summary>
/// <c>methods</c>: what checkout accepts (<c>Card</c>, <c>Pix</c>).
/// <c>provider</c>: who processes payments (<c>Fake</c>, <c>Stripe</c>).
/// <c>publishableKey</c>: for the Stripe Payment Element; null with other providers.
/// </summary>
public sealed record PaymentMethodsResponse(string Provider, IReadOnlyList<string> Methods, string? PublishableKey);
