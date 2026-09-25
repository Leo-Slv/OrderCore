namespace OrderCore.Api.Modules.Inventory.Presentation.Responses;

/// <summary>
/// Stock held for one order item. <see cref="Status"/> is <c>Reserved</c>,
/// <c>Released</c>, <c>Consumed</c>, <c>Expired</c> or <c>Returned</c>.
/// </summary>
public sealed class ReservationResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public Guid OrderId { get; init; }

    public Guid OrderItemId { get; init; }

    public int Quantity { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset ReservedAt { get; init; }

    public DateTimeOffset? ReleasedAt { get; init; }

    public DateTimeOffset? ConsumedAt { get; init; }

    public DateTimeOffset? ReturnedAt { get; init; }
}
