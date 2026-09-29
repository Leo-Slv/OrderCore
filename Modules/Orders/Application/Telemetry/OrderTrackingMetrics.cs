using System.Diagnostics.Metrics;

namespace OrderCore.Api.Modules.Orders.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Tracking</c> (Docs/specs/tracking): how many screens
/// are connected for real-time order updates, by audience (<c>customer</c>
/// or <c>admin</c>), and how many updates were pushed, by status.
/// </summary>
public sealed class OrderTrackingMetrics
{
    public const string Name = "OrderCore.Tracking";

    private readonly UpDownCounter<long> _connections;
    private readonly Counter<long> _updatesSent;

    public OrderTrackingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _connections = meter.CreateUpDownCounter<long>(
            "ordercore.tracking.connections", "{connection}", "Open connections to the order updates hub, by audience.");
        _updatesSent = meter.CreateCounter<long>(
            "ordercore.tracking.updates_sent", "{update}", "Order updates pushed to connected screens, by status.");
    }

    /// <param name="audience"><c>customer</c>, <c>admin</c> or <c>none</c> (a signed-in account that follows nothing).</param>
    public void Connected(string audience) => _connections.Add(1, new KeyValuePair<string, object?>("ordercore.audience", audience));

    public void Disconnected(string audience) => _connections.Add(-1, new KeyValuePair<string, object?>("ordercore.audience", audience));

    public void UpdateSent(string status) => _updatesSent.Add(1, new KeyValuePair<string, object?>("ordercore.order_status", status));
}
