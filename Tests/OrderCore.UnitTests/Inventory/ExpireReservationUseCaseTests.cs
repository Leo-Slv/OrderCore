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
        stockItem.TryReserve(2);
        await products.AddAsync(stockItem, CancellationToken.None);

        var reservation = InventoryReservation.Create(stockItem.ProductId, Guid.NewGuid(), Guid.NewGuid(), 2, DateTimeOffset.UtcNow);
        await reservations.AddAsync(reservation, CancellationToken.None);

        var useCase = new ExpireReservationUseCase(reservations, products, new FakeUnitOfWork());
        await useCase.ExecuteAsync(reservation.Id, CancellationToken.None);

        stockItem.QuantityReserved.Should().Be(0);
        stockItem.QuantityAvailable.Should().Be(5);
    }
}
