using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read-side/command contract Orders uses to reach the Inventory module —
/// the "Application Contract" indirection from section 7, implemented by
/// <c>InventoryServiceAdapter</c> (Infrastructure/Adapters), which wraps
/// Inventory's own use cases. See 05-orders.md.
/// </summary>
public interface IInventoryService
{
    Task<bool> TryReserveOrderItemsAsync(Order order, CancellationToken cancellationToken);

    Task ReleaseReservationsAsync(Guid orderId, CancellationToken cancellationToken);

    Task ConsumeReservationsAsync(Guid orderId, CancellationToken cancellationToken);
}
