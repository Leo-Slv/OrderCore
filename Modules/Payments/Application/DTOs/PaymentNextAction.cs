namespace OrderCore.Api.Modules.Payments.Application.DTOs;

/// <summary>
/// What the buyer still has to do for a payment to go ahead — today only
/// <see cref="ConfirmCard"/>: confirm the card in the browser (Stripe's
/// Payment Element) with <see cref="ClientSecret"/>. Handed to the buyer,
/// never stored.
/// </summary>
public sealed record PaymentNextAction(string Type, string ClientSecret)
{
    public const string ConfirmCard = "confirm_card";

    public static PaymentNextAction ConfirmCardWith(string clientSecret) => new(ConfirmCard, clientSecret);
}
