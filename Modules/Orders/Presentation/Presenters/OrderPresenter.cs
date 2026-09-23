using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Presentation.Responses;

namespace OrderCore.Api.Modules.Orders.Presentation.Presenters;

public static class OrderPresenter
{
    public static OrderResponse ToResponse(Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        Status = order.Status.ToString(),
        TotalAmount = order.TotalAmount,
        Currency = order.Currency,
        Items = order.Items.Select(ToResponse).ToList(),
    };

    /// <summary>
    /// <see cref="CreateOrderResult"/> doesn't carry <c>OrderNumber</c>/
    /// <c>Currency</c>/items (it only has what 05-orders.md's own shape
    /// lists), so this overload can't build as complete a response as
    /// <see cref="ToResponse(Order)"/> — <c>OrdersController</c> uses the
    /// <see cref="Order"/> overload for the actual create response
    /// (it re-reads the order it just created) and keeps this one only
    /// for diagram parity.
    /// </summary>
    public static OrderResponse ToResponse(CreateOrderResult result) => new()
    {
        Id = result.OrderId,
        Status = result.Status,
        TotalAmount = result.TotalAmount,
        Items = [],
    };

    private static OrderItemResponse ToResponse(OrderItem item) => new()
    {
        ProductId = item.ProductId,
        ProductName = item.ProductName,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        Total = item.Total,
    };
}
