using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Which products are low or out of stock, so Catalog's admin list can
/// filter its own products by it (backoffice decision 5).
/// </summary>
public sealed class ListProductIdsInStockStateUseCase
{
    private readonly IStockItemRepository _stockItems;

    public ListProductIdsInStockStateUseCase(IStockItemRepository stockItems)
    {
        _stockItems = stockItems;
    }

    public Task<IReadOnlyList<Guid>> ExecuteAsync(StockState state, CancellationToken cancellationToken) =>
        _stockItems.ListProductIdsInStateAsync(state, cancellationToken);
}
