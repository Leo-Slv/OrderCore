namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// An order as its buyer sees it: poll it to follow the order from
/// <c>PendingPayment</c> to <c>Confirmed</c> (or <c>PaymentFailed</c>).
/// <see cref="Payment"/> is null until payment has been requested.
/// <see cref="TotalAmount"/> = subtotal − discount + shipping + tax.
/// Internal notes are deliberately not part of this response.
/// </summary>
public sealed class OrderResponse
{
    public Guid Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public DateTimeOffset? ShippedAt { get; init; }

    public DateTimeOffset? DeliveredAt { get; init; }

    public decimal SubtotalAmount { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal ShippingAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public OrderAddressResponse? ShippingAddress { get; init; }

    public OrderAddressResponse? BillingAddress { get; init; }

    public string? CustomerNotes { get; init; }

    public IReadOnlyList<OrderItemResponse> Items { get; init; } = [];

    public OrderPaymentResponse? Payment { get; init; }
}
