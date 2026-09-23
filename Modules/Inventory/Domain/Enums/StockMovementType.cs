namespace OrderCore.Api.Modules.Inventory.Domain.Enums;

public enum StockMovementType
{
    Inbound,
    Outbound,
    Adjustment,
    ReservationCreated,
    ReservationReleased,
    ReservationConsumed,
}
