namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// Stock held for one of the order's items. <see cref="Status"/> is
/// <c>Reserved</c>, <c>Released</c>, <c>Consumed</c>, <c>Expired</c> or
/// <c>Returned</c>.
/// </summary>
public sealed class OrderReservationResponse
{
    public Guid ReservationId { get; init; }

    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset ReservedAt { get; init; }

    public DateTimeOffset? ReleasedAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }

    public DateTimeOffset? ReturnedAt { get; init; }
}
