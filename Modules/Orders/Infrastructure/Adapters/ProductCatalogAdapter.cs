using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Orders.Application.Contracts;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IProductCatalog"/> by reading from
/// Catalog's <see cref="IProductRepository"/> — the "Application Contract"
/// indirection from section 7: Orders depends on Catalog's Application
/// contract, never on Catalog's Domain/Infrastructure internals directly.
/// See 05-orders.md.
/// </summary>
public sealed class ProductCatalogAdapter : IProductCatalog
{
    private readonly IProductRepository _catalogProducts;

    public ProductCatalogAdapter(IProductRepository catalogProducts)
    {
        _catalogProducts = catalogProducts;
    }

    public Task<Product?> GetAsync(Guid productId, CancellationToken cancellationToken) =>
        _catalogProducts.GetByIdAsync(productId, cancellationToken);
}
