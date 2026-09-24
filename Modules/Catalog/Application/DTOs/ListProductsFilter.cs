namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

public sealed class ListProductsFilter
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    public Guid? CategoryId { get; init; }

    public bool? Active { get; init; }

    public string? SearchTerm { get; init; }

    /// <summary>
    /// <c>true</c> keeps only products with a compare-at price above their
    /// current price, which is what the storefront shows as a promotion.
    /// </summary>
    public bool? OnSale { get; init; }

    public ProductSortOrder Sort { get; init; } = ProductSortOrder.Name;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;
}
