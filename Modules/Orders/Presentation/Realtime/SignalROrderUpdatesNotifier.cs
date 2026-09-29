using Microsoft.AspNetCore.SignalR;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Application.Telemetry;

namespace OrderCore.Api.Modules.Orders.Presentation.Realtime;

/// <summary>
/// Sends an <see cref="OrderUpdate"/> to the order's customer and to the
/// admins through <see cref="OrderUpdatesHub"/>. One API instance is assumed
/// (spec decision 4): with several, a SignalR backplane would be needed so a
/// connection on another instance still gets it (see OrdersDependencyInjection).
/// </summary>
public sealed class SignalROrderUpdatesNotifier : IOrderUpdatesNotifier
{
    private readonly IHubContext<OrderUpdatesHub> _hub;
    private readonly OrderTrackingMetrics _metrics;

    public SignalROrderUpdatesNotifier(IHubContext<OrderUpdatesHub> hub, OrderTrackingMetrics metrics)
    {
        _hub = hub;
        _metrics = metrics;
    }

    public async Task NotifyAsync(OrderUpdate update, CancellationToken cancellationToken)
    {
        await _hub.Clients
            .Groups([OrderUpdateGroups.Customer(update.CustomerId), OrderUpdateGroups.Admins])
            .SendAsync(OrderUpdatesHub.OrderUpdatedMethod, update, cancellationToken);
        _metrics.UpdateSent(update.Status);
    }
}
