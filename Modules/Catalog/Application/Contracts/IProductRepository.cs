using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Application.Contracts;

/// <summary>
/// Persistence abstraction the Application layer depends on. The concrete
/// EF Core implementation lives in Modules/Catalog/Infrastructure/Persistence
/// — see 03-catalog.md and IOrderRepository's remarks on why this isn't a
/// generic <c>IRepository&lt;T&gt;</c>.
/// </summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken);

    Task<Product?> GetBySlugAsync(Slug slug, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the filtered products, in <see cref="ListProductsFilter.Sort"/>
    /// order, plus the total number of matches across all pages. Same
    /// shape as <c>IAuditLogRepository.ListPagedAsync</c>.
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(ListProductsFilter filter, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
