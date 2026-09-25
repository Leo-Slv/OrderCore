using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.UnitTests.Orders;

/// <summary>
/// Stock per product (0 unless set). Reserving all-or-nothing mirrors the
/// real adapter's contract; <see cref="Reserved"/> holds what is currently
/// reserved per order.
/// </summary>
internal sealed class FakeInventoryService : IInventoryService
{
    private readonly Dictionary<Guid, int> _available = new();

    public Dictionary<Guid, int> Reserved { get; } = new();

    /// <summary>Units a confirmed order consumed, per order; returning puts them back (once).</summary>
    public Dictionary<Guid, int> Consumed { get; } = new();

    public List<Guid> ReturnedOrders { get; } = new();

    public StockAlertCounts StockAlerts { get; set; } = new(0, 0);

    public void SetAvailable(Guid productId, int quantity) => _available[productId] = quantity;

    public Task<bool> TryReserveOrderItemsAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.Items.Any(i => _available.GetValueOrDefault(i.ProductId) < i.Quantity))
        {
            return Task.FromResult(false);
        }

        foreach (var item in order.Items)
        {
            _available[item.ProductId] -= item.Quantity;
        }

        Reserved[order.Id] = order.Items.Sum(i => i.Quantity);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            productIds.Distinct().ToDictionary(id => id, id => _available.GetValueOrDefault(id)));

    public Task ReleaseReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Reserved.Remove(orderId);
        return Task.CompletedTask;
    }

    public Task ConsumeReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (Reserved.Remove(orderId, out var units))
        {
            Consumed[orderId] = units;
        }

        return Task.CompletedTask;
    }

    public Task<int> ReturnConsumedStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!Consumed.Remove(orderId, out var units))
        {
            return Task.FromResult(0);
        }

        ReturnedOrders.Add(orderId);
        return Task.FromResult(units);
    }

    public Task<IReadOnlyList<OrderReservationSummary>> GetReservationsAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OrderReservationSummary>>([]);

    public Task<StockAlertCounts> GetStockAlertCountsAsync(CancellationToken cancellationToken) => Task.FromResult(StockAlerts);
}
