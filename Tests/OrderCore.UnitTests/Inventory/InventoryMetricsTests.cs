using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.Telemetry;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

/// <summary>Reservations are counted when made and when refused for lack of stock; alerts by level.</summary>
public sealed class InventoryMetricsTests : IDisposable
{
    private readonly MetricsProbe _probe = new();
    private readonly InventoryMetrics _metrics;
    private readonly FakeStockItemRepository _stockItems = new();

    public InventoryMetricsTests()
    {
        _metrics = new InventoryMetrics(_probe.Factory);
    }

    public void Dispose() => _probe.Dispose();

    private ReserveStockUseCase Reserve() => new(
        _stockItems, new FakeInventoryReservationRepository(), new FakeUnitOfWork(), new FakeAuditLogService(), _metrics, TimeProvider.System);

    [Fact]
    public async Task A_reservation_is_counted_and_one_refused_for_lack_of_stock_too()
    {
        var stockItem = StockItem.Create(Guid.NewGuid(), 1, null, DateTimeOffset.UtcNow);
        await _stockItems.AddAsync(stockItem, CancellationToken.None);

        await Reserve().ExecuteAsync(new ReserveStockCommand(stockItem.ProductId, Guid.NewGuid(), Guid.NewGuid(), 1), CancellationToken.None);
        await Reserve().ExecuteAsync(new ReserveStockCommand(stockItem.ProductId, Guid.NewGuid(), Guid.NewGuid(), 1), CancellationToken.None);

        _probe.Of("ordercore.inventory.reservations").Should().ContainSingle();
        _probe.Of("ordercore.inventory.reservations_refused").Should().ContainSingle();
    }

    [Fact]
    public async Task A_stock_alert_is_counted_by_level()
    {
        await new StockAlertMetricsRecorder(_metrics).HandleAsync(
            new StockAlertRaised(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), StockAlertLevel.OutOfStock, 0, 3),
            CancellationToken.None);

        _probe.Of("ordercore.inventory.stock_alerts").Should().ContainSingle()
            .Which.Tags.Should().Contain("ordercore.level", "OutOfStock");
    }
}
