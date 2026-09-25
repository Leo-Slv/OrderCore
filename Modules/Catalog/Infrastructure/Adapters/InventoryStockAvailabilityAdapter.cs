using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Adapters;

/// <summary>
/// Implements Catalog's <see cref="IStockAvailabilityProvider"/> (the
/// storefront's state) and <see cref="IStockLevels"/> (the backoffice's
/// numbers) by calling Inventory's use cases. This is the "Application
/// Contract" indirection from section 7, the same pattern as Orders'
/// <c>ProductCatalogAdapter</c>. Only Inventory's Application layer is
/// referenced, never its Domain, and Inventory's types are translated into
/// Catalog's here and go no further.
/// </summary>
public sealed class InventoryStockAvailabilityAdapter : IStockAvailabilityProvider, IStockLevels
{
    private readonly GetStockAvailabilityUseCase _getStockAvailability;
    private readonly EnsureStockItemUseCase _ensureStockItem;
    private readonly GetStockLevelsUseCase _getStockLevels;
    private readonly ListProductIdsInStockStateUseCase _listProductIdsInStockState;

    public InventoryStockAvailabilityAdapter(
        GetStockAvailabilityUseCase getStockAvailability,
        EnsureStockItemUseCase ensureStockItem,
        GetStockLevelsUseCase getStockLevels,
        ListProductIdsInStockStateUseCase listProductIdsInStockState)
    {
        _getStockAvailability = getStockAvailability;
        _ensureStockItem = ensureStockItem;
        _getStockLevels = getStockLevels;
        _listProductIdsInStockState = listProductIdsInStockState;
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

    public Task EnsureStockRecordAsync(Guid productId, CancellationToken cancellationToken) =>
        _ensureStockItem.ExecuteAsync(productId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, ProductStockLevel>> GetStockLevelsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var levels = await _getStockLevels.ExecuteAsync(productIds, cancellationToken);

        return levels.ToDictionary(
            s => s.ProductId,
            s => new ProductStockLevel(s.QuantityOnHand, s.QuantityReserved, s.QuantityAvailable, s.ReorderLevel, ToAvailability(s.State)));
    }

    public Task<IReadOnlyList<Guid>> ListProductIdsInStateAsync(StockAvailability state, CancellationToken cancellationToken) =>
        _listProductIdsInStockState.ExecuteAsync(ToStockState(state), cancellationToken);

    private static StockAvailability ToAvailability(StockState state) => state switch
    {
        StockState.OutOfStock => StockAvailability.OutOfStock,
        StockState.LowStock => StockAvailability.LowStock,
        _ => StockAvailability.InStock,
    };

    private static StockState ToStockState(StockAvailability availability) => availability switch
    {
        StockAvailability.OutOfStock => StockState.OutOfStock,
        StockAvailability.LowStock => StockState.LowStock,
        _ => StockState.InStock,
    };
}
