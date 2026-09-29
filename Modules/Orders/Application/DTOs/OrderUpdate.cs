namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// What is pushed to connected screens when an order changes
/// (Docs/specs/tracking/realtime-order-tracking.md): a light summary — enough
/// to update a status badge or a list row without reloading the order.
/// <see cref="ChangedAt"/> orders updates: messages can arrive out of order,
/// so a screen ignores an update older than what it already shows.
/// <see cref="Shipment"/> is set once the order shipped with tracking.
/// </summary>
public sealed record OrderUpdate(
    Guid OrderId,
    string OrderNumber,
    string Status,
    DateTimeOffset ChangedAt,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    OrderUpdateShipment? Shipment);

/// <summary>How to follow a shipped order at the carrier; any part can be missing.</summary>
public sealed record OrderUpdateShipment(string? Carrier, string? TrackingCode, string? TrackingUrl);
