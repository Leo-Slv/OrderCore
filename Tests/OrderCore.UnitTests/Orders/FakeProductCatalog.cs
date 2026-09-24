using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.UnitTests.Orders;

internal sealed class FakeProductCatalog : IProductCatalog
{
    private readonly Dictionary<Guid, CatalogProductSnapshot> _products = new();

    public CatalogProductSnapshot Add(
        decimal price, string currency = "BRL", bool isPurchasable = true, string name = "Widget")
    {
        var id = Guid.NewGuid();
        var snapshot = new CatalogProductSnapshot(
            id, $"SKU-{id:N}"[..12], name.ToLowerInvariant(), name, "https://img/widget.png", price, currency, isPurchasable);
        _products[id] = snapshot;
        return snapshot;
    }

    public Task<CatalogProductSnapshot?> GetAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_products.GetValueOrDefault(productId));

    public Task<IReadOnlyDictionary<Guid, CatalogProductSnapshot>> GetManyAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CatalogProductSnapshot>>(
            productIds.Where(_products.ContainsKey).Distinct().ToDictionary(id => id, id => _products[id]));
}
