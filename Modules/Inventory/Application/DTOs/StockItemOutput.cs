using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

public sealed record StockItemOutput(
    Guid ProductId,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    int ReorderLevel,
    StockState State,
    DateTimeOffset UpdatedAt)
{
    public static StockItemOutput From(StockItem stockItem) =>
        new(
            stockItem.ProductId,
            stockItem.QuantityOnHand,
            stockItem.QuantityReserved,
            stockItem.QuantityAvailable,
            stockItem.ReorderLevel,
            StateOf(stockItem),
            stockItem.UpdatedAt);

    public static StockState StateOf(StockItem stockItem) =>
        stockItem.QuantityAvailable <= 0 ? StockState.OutOfStock
        : stockItem.IsLowStock ? StockState.LowStock
        : StockState.InStock;
}
