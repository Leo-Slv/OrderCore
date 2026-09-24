using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Confirms an order once its payment has been authorized (see
/// <c>PaymentAuthorizedIntegrationEventHandler</c>) and consumes the stock
/// reserved for it — the reservation stops being just "held" and
/// permanently reduces on-hand stock (section 12).
/// </summary>
public sealed class ConfirmOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public ConfirmOrderUseCase(
        IOrderRepository orderRepository, IInventoryService inventoryService, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        order.Confirm(_timeProvider.GetUtcNow());
        await _inventoryService.ConsumeReservationsAsync(orderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(AuditLogActionNames.OrderConfirmed, "Order", orderId, metadata: null, userId: null, cancellationToken);
    }
}
