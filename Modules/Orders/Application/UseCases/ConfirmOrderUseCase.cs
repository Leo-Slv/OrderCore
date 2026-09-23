using OrderCore.Api.Modules.Orders.Application.Contracts;

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
    private readonly TimeProvider _timeProvider;

    public ConfirmOrderUseCase(IOrderRepository orderRepository, IInventoryService inventoryService, TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _inventoryService = inventoryService;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{orderId}' was not found.");

        order.Confirm(_timeProvider.GetUtcNow());
        await _inventoryService.ConsumeReservationsAsync(orderId, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);
    }
}
