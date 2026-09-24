using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.Contracts;

/// <summary>
/// How Catalog asks Inventory about stock, the same "Application
/// Contract" indirection as Orders' <c>IProductCatalog</c> (section 7).
/// Implemented by <c>InventoryStockAvailabilityAdapter</c>
/// (Infrastructure/Adapters). Every requested id is present in the
/// result.
/// </summary>
public interface IStockAvailabilityProvider
{
    Task<IReadOnlyDictionary<Guid, StockAvailability>> GetAvailabilityAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}

public static class StockAvailabilityProviderExtensions
{
    public static async Task<StockAvailability> GetAvailabilityAsync(
        this IStockAvailabilityProvider provider, Guid productId, CancellationToken cancellationToken)
    {
        var availability = await provider.GetAvailabilityAsync([productId], cancellationToken);
        return availability[productId];
    }
}
