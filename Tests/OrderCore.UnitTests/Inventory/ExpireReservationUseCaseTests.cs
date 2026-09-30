using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class ExpireReservationUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_releases_the_reserved_quantity_on_the_stock_item()
    {
        var products = new FakeStockItemRepository();
        var reservations = new FakeInventoryReservationRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        stockItem.TryReserve(2, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);

        var reservation = InventoryReservation.Create(stockItem.ProductId, Guid.NewGuid(), Guid.NewGuid(), 2, DateTimeOffset.UtcNow);
        await reservations.AddAsync(reservation, CancellationToken.None);

        var useCase = new ExpireReservationUseCase(reservations, products, new FakeUnitOfWork(), new FakeAuditLogService(), TestMetrics.Inventory, TimeProvider.System);
        await useCase.ExecuteAsync(reservation.Id, CancellationToken.None);

        stockItem.QuantityReserved.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(5);
    }

    [Fact]
    public void A_reservation_holds_stock_for_at_most_two_hours()
    {
        var now = DateTimeOffset.UtcNow;

        var reservation = InventoryReservation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, now);

        reservation.ExpiresAt.Should().Be(now.AddHours(2));
    }

    [Fact]
    public async Task ExecuteAsync_reports_what_it_expired_and_leaves_a_reservation_that_moved_on_alone()
    {
        var products = new FakeStockItemRepository();
        var reservations = new FakeInventoryReservationRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        stockItem.TryReserve(3, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var orderId = Guid.NewGuid();
        var held = InventoryReservation.Create(stockItem.ProductId, orderId, Guid.NewGuid(), 2, DateTimeOffset.UtcNow);
        var released = InventoryReservation.Create(stockItem.ProductId, orderId, Guid.NewGuid(), 1, DateTimeOffset.UtcNow);
        released.Release(DateTimeOffset.UtcNow);
        await reservations.AddAsync(held, CancellationToken.None);
        await reservations.AddAsync(released, CancellationToken.None);
        var useCase = new ExpireReservationUseCase(reservations, products, new FakeUnitOfWork(), new FakeAuditLogService(), TestMetrics.Inventory, TimeProvider.System);

        var expired = await useCase.ExecuteAsync(held.Id, CancellationToken.None);
        var untouched = await useCase.ExecuteAsync(released.Id, CancellationToken.None);
        var missing = await useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        expired.Should().Be(new ExpiredReservation(held.Id, orderId, stockItem.ProductId, 2));
        untouched.Should().BeNull();
        missing.Should().BeNull();
        stockItem.QuantityReserved.Should().Be(1, "only the held reservation's 2 units were released");
    }
}
