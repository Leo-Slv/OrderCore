using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Infrastructure.Jobs;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using Xunit;

namespace OrderCore.IntegrationTests.Inventory;

/// <summary>
/// The reservation safety net (Docs/specs/orders/unpaid-order-expiry.md,
/// decision 2) against a real database: a reservation still holding stock
/// past its 2-hour lifetime is expired by the job and its units are
/// available again; a consumed one is left alone.
/// </summary>
public sealed class ReservationExpiryTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public ReservationExpiryTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task A_reservation_held_past_its_lifetime_is_expired_and_its_units_come_back()
    {
        var productId = Guid.NewGuid();
        var threeHoursAgo = DateTimeOffset.UtcNow.AddHours(-3);
        InventoryReservation stale;
        InventoryReservation consumed;

        await using (var db = new InventoryDbContext(_database.Options<InventoryDbContext>()))
        {
            var stockItems = new EfStockItemRepository(db);
            var reservations = new EfInventoryReservationRepository(db);
            var unitOfWork = new InventoryUnitOfWork(db, stockItems, reservations, new NoDispatch(), TestOutboxes.Inventory(db));
            var stockItem = StockItem.Create(productId, 10, null, threeHoursAgo);
            stockItem.TryReserve(3, threeHoursAgo).Should().BeTrue();
            stockItem.TryReserve(2, threeHoursAgo).Should().BeTrue();
            stockItem.Consume(2);
            stale = InventoryReservation.Create(productId, Guid.NewGuid(), Guid.NewGuid(), 3, threeHoursAgo);
            consumed = InventoryReservation.Create(productId, Guid.NewGuid(), Guid.NewGuid(), 2, threeHoursAgo);
            consumed.Consume(threeHoursAgo);
            await stockItems.AddAsync(stockItem, CancellationToken.None);
            await reservations.AddAsync(stale, CancellationToken.None);
            await reservations.AddAsync(consumed, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var factory = _database.CreateFactory();
        var job = factory.Services.GetServices<IHostedService>().OfType<ReservationExpiryBackgroundService>().Single();
        await job.CheckAsync(CancellationToken.None);

        await using (var db = new InventoryDbContext(_database.Options<InventoryDbContext>()))
        {
            var reservations = new EfInventoryReservationRepository(db);
            (await reservations.GetByIdAsync(stale.Id, CancellationToken.None))!.Status.Should().Be(ReservationStatus.Expired);
            (await reservations.GetByIdAsync(consumed.Id, CancellationToken.None))!.Status.Should().Be(ReservationStatus.Consumed);
            var stockItem = await new EfStockItemRepository(db).GetByProductIdAsync(productId, CancellationToken.None);
            stockItem!.QuantityReserved.Should().Be(0);
            stockItem.QuantityAvailable.Should().Be(8, "10 on hand, 2 consumed earlier, the 3 held are back");
        }
    }

    private sealed class NoDispatch : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
