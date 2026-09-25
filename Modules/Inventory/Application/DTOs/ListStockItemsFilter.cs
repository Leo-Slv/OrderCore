namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

/// <summary>The backoffice stock list; without <see cref="State"/>, every stock item.</summary>
public sealed class ListStockItemsFilter
{
    public StockState? State { get; init; }

    public int Page { get; init; } = InventoryPaging.DefaultPage;

    public int PageSize { get; init; } = InventoryPaging.DefaultPageSize;
}
