namespace OrderCore.Api.Modules.Inventory.Presentation.Responses;

public sealed class StockItemResponse
{
    public Guid ProductId { get; init; }

    public int QuantityOnHand { get; init; }

    public int QuantityAvailable { get; init; }
}
