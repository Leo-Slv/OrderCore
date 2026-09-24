namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// <see cref="ExpectedTotal"/> is optional: when sent, it is the total the
/// buyer was shown, and the checkout refuses to go ahead (<c>price_changed</c>)
/// if prices moved since.
/// </summary>
public sealed record CheckoutCommand(
    Guid CustomerId,
    IReadOnlyList<CheckoutItem> Items,
    Guid ShippingAddressId,
    Guid BillingAddressId,
    PaymentMethodChoice PaymentMethod,
    string IdempotencyKey,
    string? CustomerNotes,
    decimal? ExpectedTotal);

public sealed record CheckoutItem(Guid ProductId, int Quantity);
