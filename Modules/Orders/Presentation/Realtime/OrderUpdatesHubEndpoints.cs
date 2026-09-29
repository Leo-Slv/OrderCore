namespace OrderCore.Api.Modules.Orders.Presentation.Realtime;

/// <summary>Maps the Orders module's SignalR hub, composed in Program.cs like the module's services.</summary>
public static class OrderUpdatesHubEndpoints
{
    public static IEndpointRouteBuilder MapOrderUpdatesHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<OrderUpdatesHub>(OrderUpdatesHub.Path);
        return endpoints;
    }
}
