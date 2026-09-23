using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Cancelling releases any stock still actively reserved for the order.
/// If the order was already confirmed and its reservations already
/// consumed (permanently reducing on-hand stock), there is nothing left
/// to release — <c>InventoryServiceAdapter.ReleaseReservationsAsync</c>
/// only releases reservations still in <c>Reserved</c> status and quietly
/// skips the rest, rather than throwing (returning inventory for an
/// already-fulfilled order is a restock/refund flow this feature doesn't
/// cover).
/// </summary>
public sealed class CancelOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly TimeProvider _timeProvider;

    public CancelOrderUseCase(IOrderRepository orderRepository, IInventoryService inventoryService, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{command.OrderId}' was not found.");

        order.Cancel(command.Reason, _timeProvider.GetUtcNow());
        await _inventoryService.ReleaseReservationsAsync(command.OrderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
    }
}
