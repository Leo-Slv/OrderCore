namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// What the buyer still has to do for the order's payment to go ahead —
/// Orders' own view of Payments' next action (<c>confirm_card</c>: confirm
/// the card in the browser with <see cref="ClientSecret"/>). Handed to the
/// buyer in the checkout response, never stored.
/// </summary>
public sealed record OrderPaymentNextAction(string Type, string ClientSecret);
