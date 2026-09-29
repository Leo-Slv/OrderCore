namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

/// <summary>
/// Optional details of a shipment: the carrier, its tracking code (needs the
/// carrier) and an absolute http(s) link to follow it. Send no body to ship
/// without them.
/// </summary>
public sealed class ShipOrderRequest
{
    public string? Carrier { get; init; }

    public string? TrackingCode { get; init; }

    public string? TrackingUrl { get; init; }
}
