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
/// Marks an order's payment as failed (see
/// <c>PaymentFailedIntegrationEventHandler</c>) and releases the stock
/// reserved for it back to available — the compensation flow from
/// section 12 ("Payment failed → Inventory released").
/// Like <see cref="ConfirmOrderUseCase"/>, it skips (and logs) an order that
/// is no longer <see cref="OrderStatus.PendingPayment"/> instead of retrying
/// a message that can never succeed.
/// </summary>
public sealed class MarkOrderPaymentFailedUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IAuditLogService _auditLog;
    private readonly OrdersMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MarkOrderPaymentFailedUseCase> _logger;

    public MarkOrderPaymentFailedUseCase(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IAuditLogService auditLog,
        OrdersMetrics metrics,
        TimeProvider timeProvider,
        ILogger<MarkOrderPaymentFailedUseCase> logger)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        Observed.Order(orderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        if (order.Status != OrderStatus.PendingPayment)
        {
            _logger.LogWarning(
                "Payment failed for order {OrderId}, which is {Status}; nothing to mark.", orderId, order.Status);
            return;
        }

        order.FailPayment(reason, _timeProvider.GetUtcNow());
        await _inventoryService.ReleaseReservationsAsync(orderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
        _metrics.OrderPaymentFailed();

        await _auditLog.RecordAsync(
            AuditLogActionNames.OrderPaymentFailed,
            "Order",
            orderId,
            new Dictionary<string, string?> { ["reason"] = reason },
            userId: null,
            cancellationToken);
    }
}
