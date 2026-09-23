using OrderCore.Api.Modules.Orders.Application.Contracts;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Marks an order's payment as failed (see
/// <c>PaymentFailedIntegrationEventHandler</c>) and releases the stock
/// reserved for it back to available — the compensation flow from
/// section 12 ("Payment failed → Inventory released").
/// </summary>
public sealed class MarkOrderPaymentFailedUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly TimeProvider _timeProvider;

    public MarkOrderPaymentFailedUseCase(IOrderRepository orderRepository, IInventoryService inventoryService, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.FailPayment(reason, _timeProvider.GetUtcNow());
        await _inventoryService.ReleaseReservationsAsync(orderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
    }
}
