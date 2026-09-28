namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>
/// An order's reserved units are available again (payment failed, order cancelled or the reservation expired).
/// Contract <c>inventory.stock-released</c>, version 1.
/// </summary>
public sealed record StockReleased : StockReservationIntegrationEvent
{
    public const string Name = "inventory.stock-released";
}
