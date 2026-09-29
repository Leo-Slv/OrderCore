using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Realtime;

namespace OrderCore.Api.Modules.Orders.Presentation.Realtime;

/// <summary>
/// Real-time order updates at <see cref="Path"/>
/// (Docs/specs/tracking/realtime-order-tracking.md). Any signed-in user may
/// connect — anonymous connections are refused — and joins the groups its
/// token entitles it to (<see cref="OrderUpdateGroups"/>). The hub has no
/// methods for clients to call: updates are pushed as <see cref="OrderUpdatedMethod"/>
/// by <see cref="SignalROrderUpdatesNotifier"/> when the order's events arrive.
/// </summary>
[Authorize]
public sealed class OrderUpdatesHub : Hub
{
    public const string Path = HubRoutes.Prefix + "/orders";

    /// <summary>The client method every update is sent to.</summary>
    public const string OrderUpdatedMethod = "orderUpdated";

    public override async Task OnConnectedAsync()
    {
        foreach (var group in OrderUpdateGroups.For(new PrincipalCurrentUser(Context.User)))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }
}
