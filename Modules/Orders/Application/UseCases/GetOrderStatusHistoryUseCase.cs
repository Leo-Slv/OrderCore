using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The order's recorded status transitions, oldest first: the tracking
/// screen's timeline. An unknown order is "not found", not an empty
/// timeline.
/// </summary>
public sealed class GetOrderStatusHistoryUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderStatusHistoryReader _history;

    public GetOrderStatusHistoryUseCase(IOrderRepository orderRepository, IOrderStatusHistoryReader history)
    {
        _orderRepository = orderRepository;
        _history = history;
    }

    /// <param name="requestingCustomerId">See <see cref="GetOrderDetailsUseCase.ExecuteAsync"/>.</param>
    public async Task<IReadOnlyList<OrderStatusHistoryEntry>> ExecuteAsync(
        Guid orderId, Guid? requestingCustomerId, CancellationToken cancellationToken)
    {
        await OrderAccess.LoadVisibleToAsync(_orderRepository, orderId, requestingCustomerId, cancellationToken);

        return await _history.ListAsync(orderId, cancellationToken);
    }
}
