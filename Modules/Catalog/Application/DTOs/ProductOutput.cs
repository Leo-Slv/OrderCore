using OrderCore.Api.Modules.Catalog.Domain.Enums;

using OrderCore.Api.Modules.Catalog.Domain.Entities;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// <see cref="ImageUrls"/> is not in 03-catalog.md's ProductOutput shape,
/// but ProductResponse (Presentation) has an ImageUrls field with nothing
/// else to populate it from — added here so ProductPresenter.ToResponse
/// can actually fill it.
/// </summary>
public sealed record ProductOutput(
    Guid Id, string Sku, string Name, decimal CurrentPrice, decimal? CompareAtPrice, ProductStatus Status, IReadOnlyList<string> ImageUrls)
{
    public static ProductOutput From(Product product) => new(
        product.Id, product.Sku, product.Name, product.CurrentPrice, product.CompareAtPrice, product.Status,
        product.Images.Select(i => i.Url).ToList());
}
