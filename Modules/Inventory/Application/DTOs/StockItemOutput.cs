using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

public sealed record StockItemOutput(Guid ProductId, int QuantityOnHand, int QuantityAvailable)
{
    public static StockItemOutput From(StockItem stockItem) =>
        new(stockItem.ProductId, stockItem.QuantityOnHand, stockItem.QuantityAvailable);
}
