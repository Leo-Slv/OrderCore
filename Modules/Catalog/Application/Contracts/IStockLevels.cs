using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.Contracts;

/// <summary>
/// The backoffice side of how Catalog reaches Inventory, next to the
/// storefront's <see cref="IStockAvailabilityProvider"/> (a state, never a
/// quantity). Implemented by the same <c>InventoryStockAvailabilityAdapter</c>,
/// which calls Inventory's use cases: the dependency still only goes
/// Catalog → Inventory.
/// </summary>
public interface IStockLevels
{
    /// <summary>
    /// Makes sure the product has a stock record (zero units if new) —
    /// backoffice decision 6. Idempotent.
    /// </summary>
    Task EnsureStockRecordAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Full figures for the products that have a stock record; the others are absent.</summary>
    Task<IReadOnlyDictionary<Guid, ProductStockLevel>> GetStockLevelsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>Every product whose stock is in <paramref name="state"/>.</summary>
    Task<IReadOnlyList<Guid>> ListProductIdsInStateAsync(StockAvailability state, CancellationToken cancellationToken);
}
