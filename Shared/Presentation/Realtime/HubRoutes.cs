namespace OrderCore.Api.Shared.Presentation.Realtime;

/// <summary>
/// Where SignalR hubs live (Docs/specs/tracking/realtime-order-tracking.md).
/// Browsers can't send an <c>Authorization</c> header on WebSocket or
/// server-sent-events connections, so the access token may come in the
/// <c>access_token</c> query string — but only under <see cref="Prefix"/>,
/// never on the rest of the API.
/// </summary>
public static class HubRoutes
{
    public const string Prefix = "/api/hubs";

    public const string AccessTokenQueryParameter = "access_token";
}
