using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Sets the threshold at or below which the item counts as low stock
/// (backoffice decision 4) — which is what makes the storefront's
/// <c>LowStock</c> reachable.
/// </summary>
public sealed class SetReorderLevelUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public SetReorderLevelUseCase(IStockItemRepository stockItems, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<StockItemOutput> ExecuteAsync(Guid productId, int reorderLevel, CancellationToken cancellationToken)
    {
        var stockItem = await _stockItems.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{productId}'.");

        stockItem.SetReorderLevel(reorderLevel, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StockItemOutput.From(stockItem);
    }
}
