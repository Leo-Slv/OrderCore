using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Presentation.Responses;
using OrderCore.Api.Shared.Application.DTOs;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Inventory.Presentation.Presenters;

public static class StockItemPresenter
{
    public static StockItemResponse ToResponse(StockItemOutput output) => new()
    {
        ProductId = output.ProductId,
        QuantityOnHand = output.QuantityOnHand,
        QuantityReserved = output.QuantityReserved,
        QuantityAvailable = output.QuantityAvailable,
        ReorderLevel = output.ReorderLevel,
        State = output.State.ToString(),
        UpdatedAt = output.UpdatedAt,
    };

    public static StockMovementResponse ToResponse(StockMovementOutput output) => new()
    {
        Id = output.Id,
        MovementType = output.MovementType,
        Quantity = output.Quantity,
        ReferenceType = output.ReferenceType,
        ReferenceId = output.ReferenceId,
        Reason = output.Reason,
        CreatedAt = output.CreatedAt,
    };

    public static ReservationResponse ToResponse(ReservationOutput output) => new()
    {
        Id = output.Id,
        ProductId = output.ProductId,
        OrderId = output.OrderId,
        OrderItemId = output.OrderItemId,
        Quantity = output.Quantity,
        Status = output.Status,
        ReservedAt = output.ReservedAt,
        ReleasedAt = output.ReleasedAt,
        ConsumedAt = output.ConsumedAt,
        ReturnedAt = output.ReturnedAt,
    };

    public static PagedResponse<TResponse> ToResponse<TOutput, TResponse>(PagedResult<TOutput> page, Func<TOutput, TResponse> map) => new()
    {
        Items = page.Items.Select(map).ToList(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages,
    };
}
