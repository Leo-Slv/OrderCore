namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

public sealed class ProductResponse
{
    public Guid Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal CurrentPrice { get; init; }

    public decimal? CompareAtPrice { get; init; }

    public string Status { get; init; } = string.Empty;

    public IReadOnlyList<string> ImageUrls { get; init; } = [];
}
