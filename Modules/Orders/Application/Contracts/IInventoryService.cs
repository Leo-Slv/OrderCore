using OrderCore.Api.Modules.Orders.Application.DTOs;
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
    /// <summary>
    /// Reserves every item or none: on any failure (not enough stock, no
    /// stock record, or an error) whatever was already reserved is released.
    /// Returns <c>false</c> when stock is the problem; other errors are rethrown.
    /// </summary>
    Task<bool> TryReserveOrderItemsAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Units available per product, for "is there enough for this line"
    /// decisions only; never returned to a client. Every requested id is
    /// present, with 0 when there is no stock record.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task ReleaseReservationsAsync(Guid orderId, CancellationToken cancellationToken);

    Task ConsumeReservationsAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Puts back on hand what a cancelled order had already consumed.
    /// Idempotent. Returns the number of units returned.
    /// </summary>
    Task<int> ReturnConsumedStockAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderReservationSummary>> GetReservationsAsync(Guid orderId, CancellationToken cancellationToken);

    Task<StockAlertCounts> GetStockAlertCountsAsync(CancellationToken cancellationToken);
}
