namespace OrderCore.Api.Modules.Inventory.Presentation.Responses;

/// <summary>
/// One line of a product's stock history. <see cref="MovementType"/> is
/// <c>Inbound</c>, <c>Adjustment</c> (signed <see cref="Quantity"/>),
/// <c>ReservationCreated</c>, <c>ReservationReleased</c>,
/// <c>ReservationConsumed</c> or <c>ReservationReturned</c>.
/// <see cref="ReferenceType"/>/<see cref="ReferenceId"/> point at what
/// caused it: an <c>InventoryReservation</c> or the <c>StockItem</c> itself.
/// </summary>
public sealed class StockMovementResponse
{
    public Guid Id { get; init; }

    public string MovementType { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public string? ReferenceType { get; init; }

    public Guid? ReferenceId { get; init; }

    public string? Reason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
