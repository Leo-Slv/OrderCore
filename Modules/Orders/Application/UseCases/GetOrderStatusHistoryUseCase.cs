using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

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

    public async Task<IReadOnlyList<OrderStatusHistoryEntry>> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (await _orderRepository.GetByIdAsync(orderId, cancellationToken) is null)
        {
            throw new NotFoundException("order_not_found", $"Order '{orderId}' was not found.");
        }

        return await _history.ListAsync(orderId, cancellationToken);
    }
}
