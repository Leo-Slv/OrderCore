using System.Diagnostics.Metrics;

namespace OrderCore.Api.Modules.Inventory.Application.Telemetry;

/// <summary>
/// Meter <c>OrderCore.Inventory</c> (Docs/specs/observability): reservations
/// created, released (or expired) and refused for lack of stock, and stock
/// alerts by level. Only the BCL's <see cref="System.Diagnostics.Metrics"/>.
/// </summary>
public sealed class InventoryMetrics
{
    public const string Name = "OrderCore.Inventory";

    private readonly Counter<long> _reserved;
    private readonly Counter<long> _refused;
    private readonly Counter<long> _released;
    private readonly Counter<long> _alerts;

    public InventoryMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(Name);
        _reserved = meter.CreateCounter<long>("ordercore.inventory.reservations", "{reservation}", "Reservations created.");
        _refused = meter.CreateCounter<long>(
            "ordercore.inventory.reservations_refused", "{reservation}", "Reservations refused for lack of stock.");
        _released = meter.CreateCounter<long>(
            "ordercore.inventory.reservations_released", "{reservation}", "Reservations whose units went back to available, by reason.");
        _alerts = meter.CreateCounter<long>("ordercore.inventory.stock_alerts", "{alert}", "Items entering low stock or running out.");
    }

    public void Reserved() => _reserved.Add(1);

    public void ReservationRefused() => _refused.Add(1);

    /// <param name="reason"><c>released</c> or <c>expired</c>.</param>
    public void Released(string reason) => _released.Add(1, new KeyValuePair<string, object?>("ordercore.reason", reason));

    /// <param name="level"><c>LowStock</c> or <c>OutOfStock</c>.</param>
    public void StockAlert(string level) => _alerts.Add(1, new KeyValuePair<string, object?>("ordercore.level", level));
}
