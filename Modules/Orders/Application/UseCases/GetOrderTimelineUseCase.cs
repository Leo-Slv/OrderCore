using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// The order's life across modules, oldest first, for the backoffice
/// (Docs/specs/events/async-messaging.md: admin-only; customers keep their
/// status history). An unknown order is "not found", not an empty timeline.
/// The timeline is filled asynchronously, so an event can take a moment to
/// appear after the change it records.
/// </summary>
public sealed class GetOrderTimelineUseCase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderTimelineReader _timeline;

    public GetOrderTimelineUseCase(IOrderRepository orderRepository, IOrderTimelineReader timeline)
    {
        _orderRepository = orderRepository;
        _timeline = timeline;
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await OrderAccess.LoadVisibleToAsync(_orderRepository, orderId, requestingCustomerId: null, cancellationToken);

        return await _timeline.ListAsync(orderId, cancellationToken);
    }
}
