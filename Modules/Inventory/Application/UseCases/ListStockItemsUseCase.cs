using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Stock figures by product id. The backoffice stock screen itself is
/// Catalog's admin product list, which adds names and SKUs (backoffice
/// decision 5) using <see cref="GetStockLevelsUseCase"/> and
/// <see cref="ListProductIdsInStockStateUseCase"/>.
/// </summary>
public sealed class ListStockItemsUseCase
{
    private readonly IStockItemRepository _stockItems;

    public ListStockItemsUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public async Task<PagedResult<StockItemOutput>> ExecuteAsync(ListStockItemsFilter filter, CancellationToken cancellationToken)
    {
        InventoryPaging.Validate(filter.Page, filter.PageSize);

        var (items, totalCount) = await _stockItems.ListAsync(filter.State, filter.Page, filter.PageSize, cancellationToken);

        return InventoryPaging.ToPagedResult(items.Select(StockItemOutput.From).ToList(), filter.Page, filter.PageSize, totalCount);
    }
}
