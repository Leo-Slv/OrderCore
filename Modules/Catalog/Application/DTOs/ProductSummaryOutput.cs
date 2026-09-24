using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// One product card in a listing. <see cref="PrimaryImageUrl"/> is the
/// image flagged primary, or the first in display order when none is
/// flagged (the same rule Orders already uses when it snapshots an
/// item's image).
/// </summary>
public sealed record ProductSummaryOutput(
    Guid Id,
    string Sku,
    string Slug,
    string Name,
    string? ShortDescription,
    string? Brand,
    Guid CategoryId,
    decimal CurrentPrice,
    decimal? CompareAtPrice,
    string Currency,
    ProductStatus Status,
    string? PrimaryImageUrl,
    StockAvailability Availability)
{
    public static ProductSummaryOutput From(Product product, StockAvailability availability)
    {
        var orderedImages = product.Images.OrderBy(i => i.DisplayOrder).ToList();
        var primaryImage = orderedImages.FirstOrDefault(i => i.IsPrimary) ?? orderedImages.FirstOrDefault();

        return new ProductSummaryOutput(
            product.Id,
            product.Sku,
            product.Slug.Value,
            product.Name,
            product.ShortDescription,
            product.Brand,
            product.CategoryId,
            product.CurrentPrice,
            product.CompareAtPrice,
            product.Currency,
            product.Status,
            primaryImage?.Url,
            availability);
    }
}
