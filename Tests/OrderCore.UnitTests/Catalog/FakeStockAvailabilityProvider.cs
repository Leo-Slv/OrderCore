using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// Every product is <see cref="StockAvailability.InStock"/> unless a test
/// sets something else with <see cref="Set"/>.
/// </summary>
internal sealed class FakeStockAvailabilityProvider : IStockAvailabilityProvider
{
    private readonly Dictionary<Guid, StockAvailability> _availability = new();

    public void Set(Guid productId, StockAvailability availability) => _availability[productId] = availability;

    public Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, StockAvailability>>(
            productIds.Distinct().ToDictionary(id => id, id => _availability.GetValueOrDefault(id, StockAvailability.InStock)));
}
