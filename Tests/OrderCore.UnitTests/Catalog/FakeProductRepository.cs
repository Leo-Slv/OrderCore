using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// In-memory <see cref="IProductRepository"/> used only by the use case
/// tests in this folder, so they exercise real orchestration logic without
/// needing the EF Core repository.
/// </summary>
internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public Task<Product?> GetByIdAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_products.GetValueOrDefault(productId));

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_products.Values.FirstOrDefault(p => p.Sku == sku));

    public Task<Product?> GetBySlugAsync(Slug slug, CancellationToken cancellationToken) =>
        Task.FromResult(_products.Values.FirstOrDefault(p => p.Slug == slug));

    public Task<IReadOnlyList<Product>> ListByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(_products.Values.Where(p => productIds.Contains(p.Id)).ToList());

    /// <summary>
    /// Ignores the filter and sorting, which are covered by the EF
    /// repository's integration tests; applies publishedOnly and paging.
    /// </summary>
    public Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(
        ListProductsFilter filter, bool publishedOnly, CancellationToken cancellationToken)
    {
        var matching = _products.Values.Where(p => !publishedOnly || (p.Status == ProductStatus.Active && p.Active)).ToList();
        var page = matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Product>, int)>((page, matching.Count));
    }

    /// <summary>Applies the id restriction and the status filter, newest first, and paging.</summary>
    public Task<(IReadOnlyList<Product> Items, int TotalCount)> ListForAdminAsync(
        ListAdminProductsFilter filter, IReadOnlyCollection<Guid>? onlyProductIds, CancellationToken cancellationToken)
    {
        var matching = _products.Values
            .Where(p => onlyProductIds is null || onlyProductIds.Contains(p.Id))
            .Where(p => filter.Status is null || p.Status == filter.Status)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
        var page = matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Product>, int)>((page, matching.Count));
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
