namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <see cref="Type"/> <c>confirm_card</c>: mount Stripe's Payment Element
/// with <see cref="ClientSecret"/> (and the publishable key from
/// <c>GET payments/methods</c>) and confirm the card there, 3-D Secure
/// included. The order's outcome then arrives as usual (order updates hub,
/// or <c>GET orders/{id}</c>). The secret is for the buyer's browser only.
/// </summary>
public sealed record OrderPaymentNextActionResponse(string Type, string ClientSecret);
