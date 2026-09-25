namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <c>GET admin/orders/{id}</c>: what the customer sees (<see cref="Order"/>,
/// whose <c>payment</c> is the short summary) plus the staff-only notes, the
/// customer, the payment in full and the stock reservations.
/// </summary>
public sealed class AdminOrderDetailsResponse
{
    public OrderResponse Order { get; init; } = null!;

    public string? InternalNotes { get; init; }

    public OrderCustomerResponse? Customer { get; init; }

    public OrderPaymentDetailsResponse? Payment { get; init; }

    public IReadOnlyList<OrderReservationResponse> Reservations { get; init; } = [];
}
