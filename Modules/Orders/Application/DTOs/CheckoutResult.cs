namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <param name="OrderId">The order this checkout created, or the one it had already created for this key.</param>
/// <param name="PaymentNextAction">What the buyer must do for the payment to go ahead; null when nothing (the fake provider).</param>
public sealed record CheckoutResult(Guid OrderId, OrderPaymentNextAction? PaymentNextAction);
