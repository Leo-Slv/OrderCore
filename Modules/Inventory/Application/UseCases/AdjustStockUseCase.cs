using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

public sealed class AdjustStockUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;

    public AdjustStockUseCase(IStockItemRepository stockItems, IUnitOfWork unitOfWork)
    {
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockItemOutput> ExecuteAsync(Guid productId, int quantity, string reason, CancellationToken cancellationToken)
    {
        var stockItem = await _stockItems.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException($"No stock record for product '{productId}'.");

        stockItem.Adjust(quantity, reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StockItemOutput.From(stockItem);
    }
}
