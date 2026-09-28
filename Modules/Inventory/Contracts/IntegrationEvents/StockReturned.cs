namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>
/// Units a cancelled order had consumed are back on hand.
/// Contract <c>inventory.stock-returned</c>, version 1.
/// </summary>
public sealed record StockReturned : StockReservationIntegrationEvent
{
    public const string Name = "inventory.stock-returned";
}
