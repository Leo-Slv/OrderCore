using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Full stock figures for several products in one query, for the admin
/// product list. Unlike <see cref="GetStockAvailabilityUseCase"/> (what a
/// buyer may learn), a product without a stock record is simply absent.
/// </summary>
public sealed class GetStockLevelsUseCase
{
    private readonly IStockItemRepository _stockItems;

    public GetStockLevelsUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public async Task<IReadOnlyList<StockItemOutput>> ExecuteAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var distinctIds = productIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var stockItems = await _stockItems.ListByProductIdsAsync(distinctIds, cancellationToken);
        return stockItems.Select(StockItemOutput.From).ToList();
    }
}
