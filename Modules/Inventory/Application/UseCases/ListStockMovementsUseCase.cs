using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>A product's stock history, newest first.</summary>
public sealed class ListStockMovementsUseCase
{
    private readonly IStockMovementReader _movements;

    public ListStockMovementsUseCase(IStockMovementReader movements)
    {
        _movements = movements;
    }

    public async Task<PagedResult<StockMovementOutput>> ExecuteAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        InventoryPaging.Validate(page, pageSize);

        var (items, totalCount) = await _movements.ListByProductIdAsync(productId, page, pageSize, cancellationToken);

        return InventoryPaging.ToPagedResult(items, page, pageSize, totalCount);
    }
}
