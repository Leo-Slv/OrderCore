namespace OrderCore.Api.Modules.Inventory.Presentation.Requests;

public sealed class ReceiveStockRequest
{
    /// <summary>Units that arrived; must be positive.</summary>
    public int Quantity { get; init; }

    /// <summary>Optional note kept in the movement history, e.g. a supplier invoice number.</summary>
    public string? Reason { get; init; }
}
