namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>What the admin tells about a shipment; every part optional (see <c>ShipmentDetails</c>).</summary>
public sealed record ShipmentInput(string? Carrier, string? TrackingCode, string? TrackingUrl);
