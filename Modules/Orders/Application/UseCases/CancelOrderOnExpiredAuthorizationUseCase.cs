using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Enums;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The payment's authorization expired before the order shipped (Stripe
/// spec, decision 6): nothing can be charged any more, so OrderCore cancels
/// the order itself — reason <see cref="Reason"/> — which returns its stock;
/// the settlement finds the payment already voided and does nothing more.
/// An order that has moved on (already cancelled, or shipped) is skipped and
/// logged, never an error a redelivery would repeat.
/// </summary>
public sealed class CancelOrderOnExpiredAuthorizationUseCase
{
    public const string Reason = "authorization_expired";

    private readonly IOrderRepository _orderRepository;
    private readonly CancelOrderUseCase _cancelOrder;
    private readonly ILogger<CancelOrderOnExpiredAuthorizationUseCase> _logger;

    public CancelOrderOnExpiredAuthorizationUseCase(
        IOrderRepository orderRepository, CancelOrderUseCase cancelOrder, ILogger<CancelOrderOnExpiredAuthorizationUseCase> logger)
    {
        _orderRepository = orderRepository;
        _cancelOrder = cancelOrder;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.Status is OrderStatus.Cancelled or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            _logger.LogInformation(
                "Order {OrderId} is {OrderStatus}; its expired authorization changes nothing.", orderId, order?.Status.ToString() ?? "missing");
            return;
        }

        await _cancelOrder.ExecuteAsync(new CancelOrderCommand(orderId, Reason, RequestingCustomerId: null, BySystem: true), cancellationToken);
    }
}
