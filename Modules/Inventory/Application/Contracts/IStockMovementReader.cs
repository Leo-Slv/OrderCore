using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// Read side of the movement history <c>StockMovementRecorder</c> writes —
/// same shape as Orders' <c>IOrderStatusHistoryReader</c>: movements are
/// a record, not an aggregate, so there is no repository.
/// </summary>
public interface IStockMovementReader
{
    /// <summary>One page of a product's movements, newest first, plus the total.</summary>
    Task<(IReadOnlyList<StockMovementOutput> Items, int TotalCount)> ListByProductIdAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken);
}
