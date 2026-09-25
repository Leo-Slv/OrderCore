namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

/// <summary>
/// One row of the backoffice product list (also the stock screen).
/// <see cref="Status"/> is <c>Draft</c>, <c>Active</c> or
/// <c>Discontinued</c>. <see cref="Stock"/> is null only for a product
/// without a stock record yet (created before stock records were ensured,
/// until it is published).
/// </summary>
public sealed class AdminProductSummaryResponse
{
    public Guid Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public decimal CurrentPrice { get; init; }

    public decimal? CompareAtPrice { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool Active { get; init; }

    public string? PrimaryImageUrl { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public ProductStockLevelResponse? Stock { get; init; }
}
