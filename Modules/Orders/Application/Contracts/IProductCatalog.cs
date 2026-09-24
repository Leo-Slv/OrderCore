using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read-side contract the Orders module uses to reach the Catalog module.
/// This is the "Application Contract" indirection described in section 7
/// instead of Orders referencing Catalog's persistence internals directly.
/// Returns Orders' own <see cref="CatalogProductSnapshot"/>, not Catalog's
/// domain entity.
/// </summary>
public interface IProductCatalog
{
    Task<CatalogProductSnapshot?> GetAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Products that don't exist are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<Guid, CatalogProductSnapshot>> GetManyAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}
