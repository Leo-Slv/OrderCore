namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

/// <summary>
/// One product card in <c>GET catalog/products</c>. See
/// <see cref="ProductResponse"/> for the full detail and for what
/// <see cref="Availability"/> can be.
/// </summary>
public sealed class ProductSummaryResponse
{
    public Guid Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    public string? Brand { get; init; }

    public Guid CategoryId { get; init; }

    public decimal CurrentPrice { get; init; }

    public decimal? CompareAtPrice { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? PrimaryImageUrl { get; init; }

    public string Availability { get; init; } = string.Empty;
}
