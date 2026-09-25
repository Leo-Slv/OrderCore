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

    /// <summary>Products that don't exist are simply absent from the result.</summary>
    Task<IReadOnlyList<Product>> ListByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the filtered products, in <see cref="ListProductsFilter.Sort"/>
    /// order, plus the total number of matches across all pages. Same
    /// shape as <c>IAuditLogRepository.ListPagedAsync</c>. With
    /// <paramref name="publishedOnly"/> only published, active products are
    /// considered, whatever the filter says. It is a separate argument, not
    /// part of the filter, so it can never be set from a query string.
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(
        ListProductsFilter filter, bool publishedOnly, CancellationToken cancellationToken);

    /// <summary>
    /// The backoffice list: every status, newest first. With
    /// <paramref name="onlyProductIds"/>, only those products are considered
    /// (the stock-state filter, resolved by Inventory).
    /// </summary>
    Task<(IReadOnlyList<Product> Items, int TotalCount)> ListForAdminAsync(
        ListAdminProductsFilter filter, IReadOnlyCollection<Guid>? onlyProductIds, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
