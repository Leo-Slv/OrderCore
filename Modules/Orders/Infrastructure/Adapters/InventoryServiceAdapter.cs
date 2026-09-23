using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Orders.Application.Contracts;
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
    private readonly IInventoryReservationRepository _reservations;

    public InventoryServiceAdapter(
        ReserveStockUseCase reserveStock,
        ReleaseReservationUseCase releaseReservation,
        ConsumeReservationUseCase consumeReservation,
        IInventoryReservationRepository reservations)
    {
        _reserveStock = reserveStock;
        _releaseReservation = releaseReservation;
        _consumeReservation = consumeReservation;
        _reservations = reservations;
    }

    public async Task<bool> TryReserveOrderItemsAsync(Order order, CancellationToken cancellationToken)
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

        return true;
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
}
