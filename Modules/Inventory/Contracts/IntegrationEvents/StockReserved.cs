namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>
/// Units were set aside for an order: no longer available, still on hand.
/// Contract <c>inventory.stock-reserved</c>, version 1.
/// </summary>
public sealed record StockReserved : StockReservationIntegrationEvent
{
    public const string Name = "inventory.stock-reserved";
}
