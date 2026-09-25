using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Makes sure a product has a stock record, creating it with zero units if
/// not (backoffice decision 6: every product has one, ensured by Catalog
/// when a product is created and when it is published). Idempotent: an
/// existing record is left alone, and losing a race to another request
/// creating the same one counts as success. Doesn't check that the product
/// exists — Inventory can't see the catalog; Catalog is the only caller.
/// </summary>
public sealed class EnsureStockItemUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public EnsureStockItemUseCase(IStockItemRepository stockItems, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (await _stockItems.GetByProductIdAsync(productId, cancellationToken) is not null)
        {
            return;
        }

        var stockItem = StockItem.Create(productId, initialQuantity: 0, productVariantId: null, _timeProvider.GetUtcNow());
        await _stockItems.AddAsync(stockItem, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateStockItemException)
        {
            // Created concurrently by another request: that's the outcome we wanted.
        }
    }
}
