using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
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

    /// <summary>
    /// Ignores filtering and sorting, which are covered by the EF
    /// repository's integration tests; only paging is applied.
    /// </summary>
    public Task<(IReadOnlyList<Product> Items, int TotalCount)> ListAsync(ListProductsFilter filter, CancellationToken cancellationToken)
    {
        var page = _products.Values.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return Task.FromResult<(IReadOnlyList<Product>, int)>((page, _products.Count));
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
