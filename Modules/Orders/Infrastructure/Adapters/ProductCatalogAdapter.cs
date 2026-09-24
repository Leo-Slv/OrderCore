using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IProductCatalog"/> by reading from
/// Catalog's <see cref="IProductRepository"/> — the "Application Contract"
/// indirection from section 7. Catalog's <see cref="Product"/> is turned
/// into Orders' <see cref="CatalogProductSnapshot"/> here, in
/// Infrastructure, so Orders' Application layer never sees it. See
/// 05-orders.md.
/// </summary>
public sealed class ProductCatalogAdapter : IProductCatalog
{
    private readonly IProductRepository _catalogProducts;

    public ProductCatalogAdapter(IProductRepository catalogProducts)
    {
        _catalogProducts = catalogProducts;
    }

    public async Task<CatalogProductSnapshot?> GetAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _catalogProducts.GetByIdAsync(productId, cancellationToken);
        return product is null ? null : ToSnapshot(product);
    }

    public async Task<IReadOnlyDictionary<Guid, CatalogProductSnapshot>> GetManyAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var products = await _catalogProducts.ListByIdsAsync(productIds, cancellationToken);
        return products.ToDictionary(p => p.Id, ToSnapshot);
    }

    /// <summary>
    /// Primary image: the one flagged primary, else the first in display
    /// order, the same rule as Catalog's listing cards.
    /// </summary>
    private static CatalogProductSnapshot ToSnapshot(Product product)
    {
        var orderedImages = product.Images.OrderBy(i => i.DisplayOrder).ToList();
        var primaryImage = orderedImages.FirstOrDefault(i => i.IsPrimary) ?? orderedImages.FirstOrDefault();

        return new CatalogProductSnapshot(
            product.Id,
            product.Sku,
            product.Slug.Value,
            product.Name,
            primaryImage?.Url,
            product.CurrentPrice,
            product.Currency,
            IsPurchasable: product.Status == ProductStatus.Active && product.Active);
    }
}
