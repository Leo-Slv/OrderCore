using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.Telemetry;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.ValueObjects;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Application.Observability;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The fulfilment steps an admin moves a confirmed order through:
/// start processing, ship, deliver — each recorded in the status history
/// by its domain event. Shipping captures the payment first (backoffice
/// decision 1): if the capture is refused, the order stays
/// <c>Processing</c> and the admin gets Payments' <c>409 payment_capture_failed</c>.
/// Capturing is idempotent, so shipping again after a failure that came
/// after the capture doesn't charge twice.
/// </summary>
public sealed class FulfilOrderUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuditLogService _auditLog;
    private readonly OrdersMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public FulfilOrderUseCase(
        IOrderRepository orderRepository, IPaymentGateway paymentGateway, IAuditLogService auditLog, OrdersMetrics metrics, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _paymentGateway = paymentGateway;
        _auditLog = auditLog;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public async Task<OrderDetailsOutput> StartProcessingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.StartProcessing(_timeProvider.GetUtcNow());

        return await SaveAsync(order, AuditLogActionNames.OrderProcessingStarted, cancellationToken);
    }

    public Task<OrderDetailsOutput> ShipAsync(Guid orderId, CancellationToken cancellationToken) =>
        ShipAsync(orderId, shipment: null, cancellationToken);

    /// <summary>
    /// Captures the payment, then ships, recording how to follow the order at
    /// the carrier when given. The details are validated before the capture,
    /// so a bad tracking link never leaves a captured payment behind.
    /// </summary>
    public async Task<OrderDetailsOutput> ShipAsync(Guid orderId, ShipmentInput? shipment, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.EnsureCanShip();
        var details = shipment is null ? null : ShipmentDetails.Create(shipment.Carrier, shipment.TrackingCode, shipment.TrackingUrl);
        await _paymentGateway.CaptureForOrderAsync(orderId, cancellationToken);
        order.Ship(_timeProvider.GetUtcNow(), details);

        var shipped = await SaveAsync(order, AuditLogActionNames.OrderShipped, cancellationToken);
        _metrics.OrderShipped();
        return shipped;
    }

    public async Task<OrderDetailsOutput> DeliverAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.Deliver(_timeProvider.GetUtcNow());

        var delivered = await SaveAsync(order, AuditLogActionNames.OrderDelivered, cancellationToken);
        _metrics.OrderDelivered();
        return delivered;
    }

    private async Task<Order> LoadAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Observed.Order(orderId);
        return await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");
    }

    private async Task<OrderDetailsOutput> SaveAsync(Order order, string auditAction, CancellationToken cancellationToken)
    {
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _auditLog.RecordAsync(auditAction, "Order", order.Id, metadata: null, userId: null, cancellationToken);

        var payment = await _paymentGateway.GetPaymentSummaryAsync(order.Id, cancellationToken);
        return new OrderDetailsOutput(order, payment);
    }
}
