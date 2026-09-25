using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>Low- and out-of-stock counts right now, for the dashboard.</summary>
public sealed class GetStockSummaryUseCase
{
    private readonly IStockItemRepository _stockItems;

    public GetStockSummaryUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public async Task<StockSummaryOutput> ExecuteAsync(CancellationToken cancellationToken) =>
        new(
            await _stockItems.CountInStateAsync(StockState.LowStock, cancellationToken),
            await _stockItems.CountInStateAsync(StockState.OutOfStock, cancellationToken));
}
