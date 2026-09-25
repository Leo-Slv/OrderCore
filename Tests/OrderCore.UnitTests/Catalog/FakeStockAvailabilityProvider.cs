using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// Both of Catalog's contracts with Inventory, like the real adapter.
/// Every product is <see cref="StockAvailability.InStock"/> unless a test
/// sets something else with <see cref="Set"/>; stock figures exist only for
/// products given one with <see cref="SetLevel"/>. Records which products
/// had their stock record ensured.
/// </summary>
internal sealed class FakeStockAvailabilityProvider : IStockAvailabilityProvider, IStockLevels
{
    private readonly Dictionary<Guid, StockAvailability> _availability = new();
    private readonly Dictionary<Guid, ProductStockLevel> _levels = new();

    public List<Guid> EnsuredProductIds { get; } = new();

    public void Set(Guid productId, StockAvailability availability) => _availability[productId] = availability;

    public void SetLevel(Guid productId, ProductStockLevel level) => _levels[productId] = level;

    public Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, StockAvailability>>(
            productIds.Distinct().ToDictionary(id => id, id => _availability.GetValueOrDefault(id, StockAvailability.InStock)));

    public Task EnsureStockRecordAsync(Guid productId, CancellationToken cancellationToken)
    {
        EnsuredProductIds.Add(productId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, ProductStockLevel>> GetStockLevelsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, ProductStockLevel>>(
            _levels.Where(l => productIds.Contains(l.Key)).ToDictionary(l => l.Key, l => l.Value));

    public Task<IReadOnlyList<Guid>> ListProductIdsInStateAsync(StockAvailability state, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(_levels.Where(l => l.Value.State == state).Select(l => l.Key).ToList());
}
