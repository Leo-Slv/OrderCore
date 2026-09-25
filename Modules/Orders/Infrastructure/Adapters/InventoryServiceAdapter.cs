using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' own <see cref="IInventoryService"/> by wrapping
/// Inventory's already-implemented use cases — the "Application Contract"
/// indirection from section 7, same pattern as <c>ProductCatalogAdapter</c>.
/// See 05-orders.md.
///
/// <c>OrderItem</c> has no identity of its own (see
/// <c>Docs/specs/orders/checkout-aggregate.md</c>), but
/// <see cref="ReserveStockCommand"/> requires an <c>OrderItemId</c>: a
/// fresh <see cref="Guid"/> is generated per reservation attempt purely as
/// a request-scoped value — nothing downstream joins against it, since
/// (OrderId, ProductId) is already the pair every other lookup here uses.
/// </summary>
public sealed class InventoryServiceAdapter : IInventoryService
{
    private readonly ReserveStockUseCase _reserveStock;
    private readonly ReleaseReservationUseCase _releaseReservation;
    private readonly ConsumeReservationUseCase _consumeReservation;
    private readonly GetStockAvailabilityUseCase _getStockAvailability;
    private readonly IInventoryReservationRepository _reservations;
    private readonly ReturnOrderStockUseCase _returnOrderStock;
    private readonly ListReservationsUseCase _listReservations;
    private readonly GetStockSummaryUseCase _getStockSummary;

    public InventoryServiceAdapter(
        ReserveStockUseCase reserveStock,
        ReleaseReservationUseCase releaseReservation,
        ConsumeReservationUseCase consumeReservation,
        GetStockAvailabilityUseCase getStockAvailability,
        IInventoryReservationRepository reservations,
        ReturnOrderStockUseCase returnOrderStock,
        ListReservationsUseCase listReservations,
        GetStockSummaryUseCase getStockSummary)
    {
        _reserveStock = reserveStock;
        _releaseReservation = releaseReservation;
        _consumeReservation = consumeReservation;
        _getStockAvailability = getStockAvailability;
        _reservations = reservations;
        _returnOrderStock = returnOrderStock;
        _listReservations = listReservations;
        _getStockSummary = getStockSummary;
    }

    /// <summary>
    /// Checks availability for every item first, so a product with too little
    /// stock (or no stock record at all, which <see cref="ReserveStockUseCase"/>
    /// would reject as "not found") fails before anything is reserved. The
    /// reservation loop can still lose a race with a concurrent buyer, or
    /// throw after exhausting its concurrency retries. In both cases what was
    /// already reserved is released first.
    /// </summary>
    public async Task<bool> TryReserveOrderItemsAsync(Order order, CancellationToken cancellationToken)
    {
        var available = await GetAvailableQuantitiesAsync(order.Items.Select(i => i.ProductId).ToList(), cancellationToken);
        if (order.Items.Any(i => available[i.ProductId] < i.Quantity))
        {
            return false;
        }

        try
        {
            foreach (var item in order.Items)
            {
                var command = new ReserveStockCommand(item.ProductId, order.Id, Guid.NewGuid(), item.Quantity);
                var result = await _reserveStock.ExecuteAsync(command, cancellationToken);

                if (!result.Succeeded)
                {
                    // Compensate: release whatever this attempt already
                    // reserved before reporting overall failure, so a partial
                    // reservation never lingers for an order that didn't make
                    // it to PendingPayment.
                    await ReleaseReservationsAsync(order.Id, cancellationToken);
                    return false;
                }
            }
        }
        catch
        {
            await ReleaseReservationsAsync(order.Id, cancellationToken);
            throw;
        }

        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var availability = await _getStockAvailability.ExecuteAsync(productIds, cancellationToken);
        return availability.ToDictionary(a => a.ProductId, a => a.QuantityAvailable);
    }

    public async Task ReleaseReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var reservations = await _reservations.ListByOrderIdAsync(orderId, cancellationToken);

        foreach (var reservation in reservations.Where(r => r.Status == ReservationStatus.Reserved))
        {
            await _releaseReservation.ExecuteAsync(reservation.Id, cancellationToken);
        }
    }

    public async Task ConsumeReservationsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var reservations = await _reservations.ListByOrderIdAsync(orderId, cancellationToken);

        foreach (var reservation in reservations.Where(r => r.Status == ReservationStatus.Reserved))
        {
            await _consumeReservation.ExecuteAsync(reservation.Id, cancellationToken);
        }
    }

    public Task<int> ReturnConsumedStockAsync(Guid orderId, CancellationToken cancellationToken) =>
        _returnOrderStock.ExecuteAsync(orderId, cancellationToken);

    public async Task<IReadOnlyList<OrderReservationSummary>> GetReservationsAsync(Guid orderId, CancellationToken cancellationToken) =>
        (await _listReservations.ForOrderAsync(orderId, cancellationToken))
            .Select(r => new OrderReservationSummary(
                r.Id, r.ProductId, r.Quantity, r.Status, r.ReservedAt, r.ReleasedAt, r.ConsumedAt, r.ReturnedAt))
            .ToList();

    public async Task<StockAlertCounts> GetStockAlertCountsAsync(CancellationToken cancellationToken)
    {
        var summary = await _getStockSummary.ExecuteAsync(cancellationToken);
        return new StockAlertCounts(summary.LowStockCount, summary.OutOfStockCount);
    }
}
