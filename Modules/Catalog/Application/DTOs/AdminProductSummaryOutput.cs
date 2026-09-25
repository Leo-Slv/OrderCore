using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// One row of the backoffice product list. <see cref="Stock"/> is null for
/// a product without a stock record (only possible for products created
/// before stock records were ensured, until they are published).
/// </summary>
public sealed record AdminProductSummaryOutput(
    Guid Id,
    string Sku,
    string Slug,
    string Name,
    Guid CategoryId,
    decimal CurrentPrice,
    decimal? CompareAtPrice,
    string Currency,
    ProductStatus Status,
    bool Active,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    ProductStockLevel? Stock)
{
    public static AdminProductSummaryOutput From(Product product, ProductStockLevel? stock)
    {
        var orderedImages = product.Images.OrderBy(i => i.DisplayOrder).ToList();
        var primaryImage = orderedImages.FirstOrDefault(i => i.IsPrimary) ?? orderedImages.FirstOrDefault();

        return new AdminProductSummaryOutput(
            product.Id,
            product.Sku,
            product.Slug.Value,
            product.Name,
            product.CategoryId,
            product.CurrentPrice,
            product.CompareAtPrice,
            product.Currency,
            product.Status,
            product.Active,
            primaryImage?.Url,
            product.CreatedAt,
            product.PublishedAt,
            stock);
    }
}
