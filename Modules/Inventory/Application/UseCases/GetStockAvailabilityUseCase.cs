using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Read-only availability for several products in one query. This is the
/// only Inventory entry point Catalog's and Orders' adapters call to ask
/// about stock, so neither module ever handles a <c>StockItem</c> itself.
/// A product with no stock record is reported as 0 available instead of
/// being skipped or failing: to a buyer it is simply out of stock.
/// </summary>
public sealed class GetStockAvailabilityUseCase
{
    private readonly IStockItemRepository _stockItems;

    public GetStockAvailabilityUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public async Task<IReadOnlyList<StockAvailabilityOutput>> ExecuteAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var distinctIds = productIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var stockItems = (await _stockItems.ListByProductIdsAsync(distinctIds, cancellationToken))
            .ToDictionary(s => s.ProductId);

        return distinctIds
            .Select(id => stockItems.TryGetValue(id, out var stockItem)
                ? new StockAvailabilityOutput(id, stockItem.QuantityAvailable, stockItem.IsLowStock)
                : new StockAvailabilityOutput(id, QuantityAvailable: 0, IsLowStock: false))
            .ToList();
    }
}
