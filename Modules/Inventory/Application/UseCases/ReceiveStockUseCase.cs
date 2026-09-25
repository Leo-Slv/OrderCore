using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>New units arrived; recorded as an inbound movement with the optional reason.</summary>
public sealed class ReceiveStockUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReceiveStockUseCase(IStockItemRepository stockItems, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<StockItemOutput> ExecuteAsync(Guid productId, int quantity, string? reason, CancellationToken cancellationToken)
    {
        var stockItem = await _stockItems.GetByProductIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{productId}'.");

        stockItem.Receive(quantity, reason, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return StockItemOutput.From(stockItem);
    }
}
