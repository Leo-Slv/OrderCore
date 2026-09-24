using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

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
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CancelOrderUseCase(
        IOrderRepository orderRepository, IInventoryService inventoryService, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{command.OrderId}' was not found.");

        order.Cancel(command.Reason, _timeProvider.GetUtcNow());
        await _inventoryService.ReleaseReservationsAsync(command.OrderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.OrderCancelled,
            "Order",
            command.OrderId,
            new Dictionary<string, string?> { ["reason"] = command.Reason },
            userId: null,
            cancellationToken);
    }
}
