using OrderCore.Api.Modules.Catalog.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read-side contract the Orders module uses to reach the Catalog module.
/// This is the "Application Contract" indirection described in section 7
/// instead of Orders referencing Catalog's persistence internals directly.
/// </summary>
public interface IProductCatalog
{
    Task<Product?> GetAsync(Guid productId, CancellationToken cancellationToken);
}
