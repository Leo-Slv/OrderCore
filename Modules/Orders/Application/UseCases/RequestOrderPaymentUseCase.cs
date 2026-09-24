using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Reserves stock for every item, moves the order to PendingPayment, and
/// kicks off payment — see 05-orders.md's "Fluxo de checkout". Confirming
/// or failing the order happens later, asynchronously, when
/// <c>PaymentAuthorizedIntegrationEventHandler</c>/
/// <c>PaymentFailedIntegrationEventHandler</c> react to the outbox
/// publishing Payments' outcome — this use case doesn't wait for it, even
/// though today's only provider (<c>FakePaymentProvider</c>) happens to
/// resolve synchronously.
/// </summary>
public sealed class RequestOrderPaymentUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _timeProvider;

    public RequestOrderPaymentUseCase(
        IOrderRepository orderRepository, IInventoryService inventoryService, IPaymentGateway paymentGateway, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _paymentGateway = paymentGateway;
        _timeProvider = timeProvider;
    }

    public async Task<CreateOrderResult> ExecuteAsync(Guid orderId, PaymentMethodChoice paymentMethod, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");

        var reserved = await _inventoryService.TryReserveOrderItemsAsync(order, cancellationToken);
        if (!reserved)
        {
            throw new ConflictException("insufficient_stock", $"Could not reserve stock for order '{orderId}'.");
        }

        var now = _timeProvider.GetUtcNow();
        order.RequestPayment(now);

        // OrderId doubles as the idempotency key: PaymentPersistenceModel
        // enforces a unique index on it, so retrying this use case for the
        // same order can never create two payments.
        await _paymentGateway.RequestPaymentAsync(
            order.Id, order.TotalAmount, order.Currency, paymentMethod, order.Id.ToString(), cancellationToken);

        await _orderRepository.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id, order.TotalAmount, order.Status.ToString());
    }
}
