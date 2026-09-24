namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// Product name, SKU, image and unit price are snapshots taken when the
/// order was placed. They don't follow later catalog changes.
/// </summary>
public sealed class OrderItemResponse
{
    public Guid ProductId { get; init; }

    public string ProductSku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string? ProductImageUrl { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Total { get; init; }
}
