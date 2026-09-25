using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

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
    private readonly TimeProvider _timeProvider;

    public FulfilOrderUseCase(
        IOrderRepository orderRepository, IPaymentGateway paymentGateway, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _paymentGateway = paymentGateway;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<OrderDetailsOutput> StartProcessingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.StartProcessing(_timeProvider.GetUtcNow());

        return await SaveAsync(order, AuditLogActionNames.OrderProcessingStarted, cancellationToken);
    }

    public async Task<OrderDetailsOutput> ShipAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.EnsureCanShip();
        await _paymentGateway.CaptureForOrderAsync(orderId, cancellationToken);
        order.Ship(_timeProvider.GetUtcNow());

        return await SaveAsync(order, AuditLogActionNames.OrderShipped, cancellationToken);
    }

    public async Task<OrderDetailsOutput> DeliverAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(orderId, cancellationToken);

        order.Deliver(_timeProvider.GetUtcNow());

        return await SaveAsync(order, AuditLogActionNames.OrderDelivered, cancellationToken);
    }

    private async Task<Order> LoadAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

    private async Task<OrderDetailsOutput> SaveAsync(Order order, string auditAction, CancellationToken cancellationToken)
    {
        await _orderRepository.SaveChangesAsync(cancellationToken);
        await _auditLog.RecordAsync(auditAction, "Order", order.Id, metadata: null, userId: null, cancellationToken);

        var payment = await _paymentGateway.GetPaymentSummaryAsync(order.Id, cancellationToken);
        return new OrderDetailsOutput(order, payment);
    }
}
