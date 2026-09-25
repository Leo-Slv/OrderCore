using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Inventory;

/// <summary>
/// The backoffice side of Inventory against real PostgreSQL: the stock
/// states computed in SQL agree with the domain's rule, manual changes and
/// returns reach the movement history (through the real
/// <see cref="StockMovementRecorder"/>), and ensuring a stock record is
/// safe to repeat and to race.
/// </summary>
public sealed class StockBackofficePersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private InventoryDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    /// <summary>Hands movement events to the real recorder, as the app's dispatcher does.</summary>
    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        private readonly StockMovementRecorder _recorder;

        public RecordingDispatcher(InventoryDbContext dbContext)
        {
            _recorder = new StockMovementRecorder(dbContext);
        }

        public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
        {
            foreach (var movement in events.OfType<InventoryStockMovementRecorded>())
            {
                await _recorder.HandleAsync(movement, cancellationToken);
            }
        }
    }

    /// <summary>One unit of work's worth of Inventory, over its own context.</summary>
    private sealed class Scope : IAsyncDisposable
    {
        public Scope(InventoryDbContext dbContext)
        {
            DbContext = dbContext;
            StockItems = new EfStockItemRepository(dbContext);
            Reservations = new EfInventoryReservationRepository(dbContext);
            UnitOfWork = new InventoryUnitOfWork(dbContext, StockItems, Reservations, new RecordingDispatcher(dbContext));
        }

        public InventoryDbContext DbContext { get; }

        public EfStockItemRepository StockItems { get; }

        public EfInventoryReservationRepository Reservations { get; }

        public InventoryUnitOfWork UnitOfWork { get; }

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }

    private Scope NewScope() => new(CreateDbContext());

    private async Task<Guid> SaveStockItemAsync(int onHand, int reorderLevel = 0, int reserved = 0)
    {
        await using var scope = NewScope();
        var stockItem = StockItem.Create(Guid.NewGuid(), onHand, null, Now);
        stockItem.SetReorderLevel(reorderLevel, Now);
        if (reserved > 0)
        {
            stockItem.TryReserve(reserved).Should().BeTrue();
        }

        await scope.StockItems.AddAsync(stockItem, CancellationToken.None);
        await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        return stockItem.ProductId;
    }

    [Fact]
    public async Task Stock_states_computed_in_sql_agree_with_the_domain_rule()
    {
        var outOfStock = await SaveStockItemAsync(onHand: 0);
        var fullyReserved = await SaveStockItemAsync(onHand: 2, reserved: 2);
        var low = await SaveStockItemAsync(onHand: 3, reorderLevel: 3);
        var lowAfterReservations = await SaveStockItemAsync(onHand: 10, reorderLevel: 4, reserved: 7);
        var inStock = await SaveStockItemAsync(onHand: 9, reorderLevel: 2);

        await using var scope = NewScope();

        (await scope.StockItems.ListProductIdsInStateAsync(StockState.OutOfStock, CancellationToken.None))
            .Should().BeEquivalentTo([outOfStock, fullyReserved]);
        (await scope.StockItems.ListProductIdsInStateAsync(StockState.LowStock, CancellationToken.None))
            .Should().BeEquivalentTo([low, lowAfterReservations]);
        (await scope.StockItems.ListProductIdsInStateAsync(StockState.InStock, CancellationToken.None))
            .Should().BeEquivalentTo([inStock]);
        (await scope.StockItems.CountInStateAsync(StockState.LowStock, CancellationToken.None)).Should().Be(2);

        var (lowPage, lowTotal) = await scope.StockItems.ListAsync(StockState.LowStock, 1, 20, CancellationToken.None);
        lowTotal.Should().Be(2);
        lowPage.Should().OnlyContain(s => StockItemOutput.StateOf(s) == StockState.LowStock);

        var all = await scope.StockItems.ListByProductIdsAsync([outOfStock, fullyReserved, low, lowAfterReservations, inStock], CancellationToken.None);
        all.Should().HaveCount(5);
    }

    [Fact]
    public async Task Receiving_and_adjusting_are_recorded_in_the_movement_history_with_their_reasons()
    {
        var productId = await SaveStockItemAsync(onHand: 0);

        await using (var scope = NewScope())
        {
            await new ReceiveStockUseCase(scope.StockItems, scope.UnitOfWork, TimeProvider.System)
                .ExecuteAsync(productId, 12, "invoice 889", CancellationToken.None);
        }

        await using (var scope = NewScope())
        {
            await new AdjustStockUseCase(scope.StockItems, scope.UnitOfWork, TimeProvider.System)
                .ExecuteAsync(productId, -2, "broken in the warehouse", CancellationToken.None);
        }

        await using (var scope = NewScope())
        {
            var history = await new ListStockMovementsUseCase(new EfStockMovementReader(scope.DbContext))
                .ExecuteAsync(productId, page: 1, pageSize: 20, CancellationToken.None);

            history.TotalItems.Should().Be(2);
            history.Items.Select(m => (m.MovementType, m.Quantity, m.Reason)).Should().Equal(
                ("Adjustment", -2, "broken in the warehouse"),
                ("Inbound", 12, "invoice 889"));

            var stock = await scope.StockItems.GetByProductIdAsync(productId, CancellationToken.None);
            stock!.QuantityOnHand.Should().Be(10);
        }
    }

    [Fact]
    public async Task Returning_a_cancelled_orders_consumed_stock_persists_and_is_recorded()
    {
        var orderId = Guid.NewGuid();
        var productId = await SaveStockItemAsync(onHand: 5);
        Guid reservationId;

        await using (var scope = NewScope())
        {
            var stockItem = await scope.StockItems.GetByProductIdAsync(productId, CancellationToken.None);
            stockItem!.TryReserve(2);
            stockItem.Consume(2);
            var reservation = InventoryReservation.Create(productId, orderId, Guid.NewGuid(), 2, Now);
            reservation.Consume(Now);
            reservationId = reservation.Id;
            await scope.Reservations.AddAsync(reservation, CancellationToken.None);
            await scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = NewScope())
        {
            var returned = await new ReturnOrderStockUseCase(scope.StockItems, scope.Reservations, scope.UnitOfWork, TimeProvider.System)
                .ExecuteAsync(orderId, CancellationToken.None);
            returned.Should().Be(2);
        }

        await using (var scope = NewScope())
        {
            (await scope.StockItems.GetByProductIdAsync(productId, CancellationToken.None))!.QuantityOnHand.Should().Be(5);

            var reservation = await scope.Reservations.GetByIdAsync(reservationId, CancellationToken.None);
            reservation!.Status.Should().Be(ReservationStatus.Returned);
            reservation.ReturnedAt.Should().NotBeNull();

            var history = await new EfStockMovementReader(scope.DbContext).ListByProductIdAsync(productId, 1, 20, CancellationToken.None);
            history.Items.Should().Contain(m => m.MovementType == "ReservationReturned" && m.Quantity == 2 && m.ReferenceId == reservationId);

            var reservations = await new ListReservationsUseCase(scope.Reservations).ForProductAsync(productId, 1, 20, CancellationToken.None);
            reservations.Items.Should().ContainSingle().Which.Status.Should().Be("Returned");
        }
    }

    [Fact]
    public async Task Ensuring_a_stock_record_is_idempotent_and_survives_a_race()
    {
        var productId = Guid.NewGuid();

        await using (var scope = NewScope())
        {
            await new EnsureStockItemUseCase(scope.StockItems, scope.UnitOfWork, TimeProvider.System).ExecuteAsync(productId, CancellationToken.None);
        }

        await using (var scope = NewScope())
        {
            await new EnsureStockItemUseCase(scope.StockItems, scope.UnitOfWork, TimeProvider.System).ExecuteAsync(productId, CancellationToken.None);
        }

        // The race: this scope decided the record was missing, but another
        // request saved it first.
        await using (var scope = NewScope())
        {
            await scope.StockItems.AddAsync(StockItem.Create(productId, 0, null, Now), CancellationToken.None);
            var act = () => scope.UnitOfWork.SaveChangesAsync(CancellationToken.None);
            await act.Should().ThrowAsync<DuplicateStockItemException>();
        }

        await using (var scope = NewScope())
        {
            (await scope.DbContext.StockItems.CountAsync(s => s.ProductId == productId)).Should().Be(1);
        }
    }
}
