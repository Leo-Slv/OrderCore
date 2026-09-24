using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Adapters;

/// <summary>
/// Implements Catalog's <see cref="IStockAvailabilityProvider"/> by calling
/// Inventory's <see cref="GetStockAvailabilityUseCase"/>. This is the
/// "Application Contract" indirection from section 7, the same pattern as
/// Orders' <c>ProductCatalogAdapter</c>. Only Inventory's Application layer
/// is referenced, never its Domain. The available quantity is turned into
/// a state here and goes no further.
/// </summary>
public sealed class InventoryStockAvailabilityAdapter : IStockAvailabilityProvider
{
    private readonly GetStockAvailabilityUseCase _getStockAvailability;

    public InventoryStockAvailabilityAdapter(GetStockAvailabilityUseCase getStockAvailability)
    {
        _getStockAvailability = getStockAvailability;
    }

    public async Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var availability = await _getStockAvailability.ExecuteAsync(productIds, cancellationToken);

        return availability.ToDictionary(
            a => a.ProductId,
            a => a.QuantityAvailable <= 0 ? StockAvailability.OutOfStock
                : a.IsLowStock ? StockAvailability.LowStock
                : StockAvailability.InStock);
    }
}
