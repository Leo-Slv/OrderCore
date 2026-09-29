using Microsoft.AspNetCore.SignalR;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

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

    public SignalROrderUpdatesNotifier(IHubContext<OrderUpdatesHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyAsync(OrderUpdate update, CancellationToken cancellationToken) =>
        _hub.Clients
            .Groups([OrderUpdateGroups.Customer(update.CustomerId), OrderUpdateGroups.Admins])
            .SendAsync(OrderUpdatesHub.OrderUpdatedMethod, update, cancellationToken);
}
