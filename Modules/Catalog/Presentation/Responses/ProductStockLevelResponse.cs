namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

/// <summary>
/// A product's stock for the backoffice. <see cref="QuantityAvailable"/> is
/// on hand minus reserved; <see cref="State"/> is <c>InStock</c>,
/// <c>LowStock</c> (available at or below <see cref="ReorderLevel"/>) or
/// <c>OutOfStock</c> — the same state the storefront sees as availability.
/// </summary>
public sealed class ProductStockLevelResponse
{
    public int QuantityOnHand { get; init; }

    public int QuantityReserved { get; init; }

    public int QuantityAvailable { get; init; }

    public int ReorderLevel { get; init; }

    public string State { get; init; } = string.Empty;
}
