using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Presentation.Responses;

namespace OrderCore.Api.Modules.Inventory.Presentation.Presenters;

public static class StockItemPresenter
{
    public static StockItemResponse ToResponse(StockItemOutput output) => new()
    {
        ProductId = output.ProductId,
        QuantityOnHand = output.QuantityOnHand,
        QuantityAvailable = output.QuantityAvailable,
    };
}
