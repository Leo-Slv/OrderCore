using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>One row of a customer's order history.</summary>
public sealed record OrderSummaryOutput(
    Guid Id, string OrderNumber, OrderStatus Status, DateTimeOffset CreatedAt, decimal TotalAmount, string Currency, int ItemCount)
{
    public static OrderSummaryOutput From(Order order) => new(
        order.Id, order.OrderNumber, order.Status, order.CreatedAt, order.TotalAmount, order.Currency, order.Items.Sum(i => i.Quantity));
}
