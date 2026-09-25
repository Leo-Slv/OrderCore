using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The order plus its payment status. This is what the tracking screen
/// polls while the payment outcome is still on its way through the
/// outbox.
/// </summary>
public sealed class GetOrderDetailsUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentGateway _paymentGateway;

    public GetOrderDetailsUseCase(IOrderRepository orderRepository, IPaymentGateway paymentGateway)
    {
        _orderRepository = orderRepository;
        _paymentGateway = paymentGateway;
    }

    /// <param name="requestingCustomerId">
    /// The customer asking, or null for an admin. A customer asking for
    /// someone else's order gets <c>order_not_found</c>, the same as for an
    /// order that doesn't exist.
    /// </param>
    public async Task<OrderDetailsOutput> ExecuteAsync(Guid orderId, Guid? requestingCustomerId, CancellationToken cancellationToken)
    {
        var order = await OrderAccess.LoadVisibleToAsync(_orderRepository, orderId, requestingCustomerId, cancellationToken);

        var payment = await _paymentGateway.GetPaymentSummaryAsync(orderId, cancellationToken);

        return new OrderDetailsOutput(order, payment);
    }
}
