using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;

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

    public Task<IReadOnlyList<Product>> ListAsync(ListProductsFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(_products.Values.ToList());

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
