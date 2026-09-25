namespace OrderCore.Api.Modules.Inventory.Domain.Enums;

public enum StockMovementType
{
    Inbound,
    Outbound,
    Adjustment,
    ReservationCreated,
    ReservationReleased,
    ReservationConsumed,

    /// <summary>
    /// Units a cancelled order had already consumed, put back on hand.
    /// </summary>
    ReservationReturned,
}
