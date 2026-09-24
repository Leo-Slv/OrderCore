using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

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

    public async Task<OrderDetailsOutput> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        var payment = await _paymentGateway.GetPaymentSummaryAsync(orderId, cancellationToken);

        return new OrderDetailsOutput(order, payment);
    }
}
