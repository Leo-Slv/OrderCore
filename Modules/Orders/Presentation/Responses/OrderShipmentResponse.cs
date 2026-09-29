namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>How to follow a shipped order at the carrier; any part can be missing.</summary>
public sealed record OrderShipmentResponse(string? Carrier, string? TrackingCode, string? TrackingUrl);
