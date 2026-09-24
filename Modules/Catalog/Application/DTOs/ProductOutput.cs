using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// Full product detail: what a product page renders and what the
/// admin create/update/publish endpoints return. <see cref="Images"/> are
/// in display order; inactive variants are left out, since they cannot be
/// bought. For the lighter listing shape, see
/// <see cref="ProductSummaryOutput"/>.
/// </summary>
public sealed record ProductOutput(
    Guid Id,
    string Sku,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Brand,
    Guid CategoryId,
    decimal CurrentPrice,
    decimal? CompareAtPrice,
    string Currency,
    ProductStatus Status,
    IReadOnlyList<ProductImageOutput> Images,
    IReadOnlyList<ProductVariantOutput> Variants,
    StockAvailability Availability)
{
    public static ProductOutput From(Product product, StockAvailability availability) => new(
        product.Id,
        product.Sku,
        product.Slug.Value,
        product.Name,
        product.ShortDescription,
        product.Description,
        product.Brand,
        product.CategoryId,
        product.CurrentPrice,
        product.CompareAtPrice,
        product.Currency,
        product.Status,
        product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageOutput(i.Id, i.Url, i.AltText, i.IsPrimary, i.DisplayOrder))
            .ToList(),
        product.Variants
            .Where(v => v.Active)
            .Select(v => new ProductVariantOutput(v.Id, v.Sku, v.Name, v.AttributesJson, v.AdditionalPrice))
            .ToList(),
        availability);
}

public sealed record ProductImageOutput(Guid Id, string Url, string? AltText, bool IsPrimary, int DisplayOrder);

public sealed record ProductVariantOutput(Guid Id, string Sku, string Name, string AttributesJson, decimal AdditionalPrice);
