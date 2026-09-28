using Microsoft.Extensions.Logging;
using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Confirms an order once its payment has been authorized (see
/// <c>PaymentAuthorizedIntegrationEventHandler</c>) and consumes the stock
/// reserved for it — the reservation stops being just "held" and
/// permanently reduces on-hand stock (section 12).
/// <para>
/// Runs from a message handler, which retries a failing message five times
/// before parking it in the failed-message list. An order that is no
/// longer <see cref="OrderStatus.PendingPayment"/> (an admin cancelled it,
/// and the payment was voided then) is logged and skipped: throwing would
/// only retry, then park, a message that can never succeed.
/// </para>
/// </summary>
public sealed class ConfirmOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditLogService _auditLog;
    private readonly OrdersMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConfirmOrderUseCase> _logger;

    public ConfirmOrderUseCase(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IAuditLogService auditLog,
        OrdersMetrics metrics,
        TimeProvider timeProvider,
        ILogger<ConfirmOrderUseCase> logger)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Observed.Order(orderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        if (order.Status != OrderStatus.PendingPayment)
        {
            _logger.LogWarning(
                "Payment authorized for order {OrderId}, which is {Status}; nothing to confirm.", orderId, order.Status);
            return;
        }

        order.Confirm(_timeProvider.GetUtcNow());
        await _inventoryService.ConsumeReservationsAsync(orderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        _metrics.OrderConfirmed(order.TotalAmount, order.Currency);

        await _auditLog.RecordAsync(AuditLogActionNames.OrderConfirmed, "Order", orderId, metadata: null, userId: null, cancellationToken);
    }
}
