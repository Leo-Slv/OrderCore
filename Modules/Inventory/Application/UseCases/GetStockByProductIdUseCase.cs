using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

public sealed class GetStockByProductIdUseCase
{
    private readonly IStockItemRepository _stockItems;

    public GetStockByProductIdUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public async Task<StockItemOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var stockItem = await _stockItems.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{productId}'.");

        return StockItemOutput.From(stockItem);
    }
}
