namespace OrderCore.Api.Modules.Inventory.Presentation.Responses;

/// <summary>
/// <see cref="QuantityAvailable"/> is on hand minus reserved.
/// <see cref="State"/> is <c>InStock</c>, <c>LowStock</c> (available at or
/// below <see cref="ReorderLevel"/>) or <c>OutOfStock</c>.
/// </summary>
public sealed class StockItemResponse
{
    public Guid ProductId { get; init; }

    public int QuantityOnHand { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityAvailable { get; init; }

    public int ReorderLevel { get; init; }

    public string State { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; }
}
