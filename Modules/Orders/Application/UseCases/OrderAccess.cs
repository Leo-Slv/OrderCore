using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Who may see an order: its own customer, or an admin (no requesting
/// customer). Anyone else gets the same <c>order_not_found</c> as for an
/// order that doesn't exist, so order ids can't be probed.
/// </summary>
internal static class OrderAccess
{
    public static async Task<Order> LoadVisibleToAsync(
        IOrderRepository orders, Guid orderId, Guid? requestingCustomerId, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || (requestingCustomerId is { } customerId && order.CustomerId != customerId))
        {
            throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");
        }

        return order;
    }
}
