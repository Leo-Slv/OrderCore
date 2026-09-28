namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>
/// An order's reserved units left the stock for good (the order was confirmed).
/// Contract <c>inventory.stock-consumed</c>, version 1.
/// </summary>
public sealed record StockConsumed : StockReservationIntegrationEvent
{
    public const string Name = "inventory.stock-consumed";
}
