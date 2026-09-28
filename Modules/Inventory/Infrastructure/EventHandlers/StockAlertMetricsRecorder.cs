using OrderCore.Api.Modules.Inventory.Application.Telemetry;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers;

/// <summary>
/// Counts stock alerts (<see cref="InventoryMetrics.StockAlert"/>). Alerts
/// can come from any operation that changes what is available — reserving,
/// releasing, receiving, adjusting, a new reorder level — so they are counted
/// where they all pass: the domain event, dispatched once the change was saved.
/// </summary>
public sealed class StockAlertMetricsRecorder : IDomainEventHandler<StockAlertRaised>
{
    private readonly InventoryMetrics _metrics;

    public StockAlertMetricsRecorder(InventoryMetrics metrics)
    {
        _metrics = metrics;
    }

    public Task HandleAsync(StockAlertRaised domainEvent, CancellationToken cancellationToken)
    {
        _metrics.StockAlert(domainEvent.Level.ToString());
        return Task.CompletedTask;
    }
}
