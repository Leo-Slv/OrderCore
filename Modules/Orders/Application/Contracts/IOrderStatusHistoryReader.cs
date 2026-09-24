using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read side of <c>order_status_history</c>, which
/// <c>OrderStatusHistoryProjector</c> fills from Order's domain events.
/// Entries come back in the order they were recorded.
/// </summary>
public interface IOrderStatusHistoryReader
{
    Task<IReadOnlyList<OrderStatusHistoryEntry>> ListAsync(Guid orderId, CancellationToken cancellationToken);
}
