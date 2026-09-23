namespace OrderCore.Api.Modules.Inventory.Presentation.Requests;

public sealed class AdjustStockRequest
{
    public int Quantity { get; init; }

    public string Reason { get; init; } = string.Empty;
}
