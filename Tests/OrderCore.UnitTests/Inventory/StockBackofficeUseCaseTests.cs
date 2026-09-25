using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

/// <summary>
/// The backoffice side of Inventory: ensuring a stock record exists,
/// receiving, the reorder level, returning a cancelled order's stock, and
/// the state every listing is filtered by.
/// </summary>
public sealed class StockBackofficeUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly FakeStockItemRepository _stockItems = new();
    private readonly FakeInventoryReservationRepository _reservations = new();

    private async Task<StockItem> SaveStockItemAsync(int onHand, int reorderLevel = 0)
    {
        var stockItem = StockItem.Create(Guid.NewGuid(), onHand, null, Now);
        stockItem.SetReorderLevel(reorderLevel, Now);
        await _stockItems.AddAsync(stockItem, CancellationToken.None);
        return stockItem;
    }

    /// <summary>A reservation of <paramref name="quantity"/> units, already consumed (the order was confirmed).</summary>
    private async Task<InventoryReservation> SaveConsumedReservationAsync(StockItem stockItem, Guid orderId, int quantity)
    {
        stockItem.TryReserve(quantity);
        stockItem.Consume(quantity);
        var reservation = InventoryReservation.Create(stockItem.ProductId, orderId, Guid.NewGuid(), quantity, Now);
        reservation.Consume(Now);
        await _reservations.AddAsync(reservation, CancellationToken.None);
        return reservation;
    }

    [Fact]
    public async Task Ensuring_a_missing_stock_record_creates_it_empty()
    {
        var productId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();

        await new EnsureStockItemUseCase(_stockItems, unitOfWork, TimeProvider.System).ExecuteAsync(productId, CancellationToken.None);

        var created = await _stockItems.GetByProductIdAsync(productId, CancellationToken.None);
        created!.QuantityOnHand.Should().Be(0);
        unitOfWork.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Ensuring_an_existing_stock_record_changes_nothing()
    {
        var existing = await SaveStockItemAsync(onHand: 8);
        var unitOfWork = new FakeUnitOfWork();

        await new EnsureStockItemUseCase(_stockItems, unitOfWork, TimeProvider.System).ExecuteAsync(existing.ProductId, CancellationToken.None);

        existing.QuantityOnHand.Should().Be(8);
        unitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Losing_the_race_to_create_a_stock_record_counts_as_success()
    {
        var useCase = new EnsureStockItemUseCase(_stockItems, FakeUnitOfWork.ThatReportsADuplicateStockItem(), TimeProvider.System);

        var act = () => useCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Receiving_stock_for_a_product_without_a_record_is_not_found()
    {
        var act = () => new ReceiveStockUseCase(_stockItems, new FakeUnitOfWork(), TimeProvider.System)
            .ExecuteAsync(Guid.NewGuid(), 5, null, CancellationToken.None);

        (await act.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("stock_item_not_found");
    }

    [Fact]
    public async Task Receiving_stock_and_setting_a_reorder_level_report_the_new_state()
    {
        var stockItem = await SaveStockItemAsync(onHand: 0);

        var received = await new ReceiveStockUseCase(_stockItems, new FakeUnitOfWork(), TimeProvider.System)
            .ExecuteAsync(stockItem.ProductId, 4, "first delivery", CancellationToken.None);
        received.State.Should().Be(StockState.InStock);

        var withLevel = await new SetReorderLevelUseCase(_stockItems, new FakeUnitOfWork(), TimeProvider.System)
            .ExecuteAsync(stockItem.ProductId, 4, CancellationToken.None);
        withLevel.State.Should().Be(StockState.LowStock);
        withLevel.ReorderLevel.Should().Be(4);
    }

    [Theory]
    [InlineData(0, 0, StockState.OutOfStock)]
    [InlineData(3, 0, StockState.InStock)]
    [InlineData(3, 3, StockState.LowStock)]
    [InlineData(4, 3, StockState.InStock)]
    public async Task The_state_follows_available_units_and_the_reorder_level(int onHand, int reorderLevel, StockState expected)
    {
        var stockItem = await SaveStockItemAsync(onHand, reorderLevel);

        StockItemOutput.From(stockItem).State.Should().Be(expected);
    }

    [Fact]
    public async Task Fully_reserved_stock_is_out_of_stock()
    {
        var stockItem = await SaveStockItemAsync(onHand: 2);
        stockItem.TryReserve(2);

        StockItemOutput.From(stockItem).State.Should().Be(StockState.OutOfStock);
    }

    [Fact]
    public async Task The_summary_counts_low_and_out_of_stock_items()
    {
        await SaveStockItemAsync(onHand: 0);
        await SaveStockItemAsync(onHand: 2, reorderLevel: 5);
        await SaveStockItemAsync(onHand: 1, reorderLevel: 1);
        await SaveStockItemAsync(onHand: 10, reorderLevel: 1);

        var summary = await new GetStockSummaryUseCase(_stockItems).ExecuteAsync(CancellationToken.None);

        summary.Should().Be(new StockSummaryOutput(LowStockCount: 2, OutOfStockCount: 1));
    }

    [Fact]
    public async Task Returning_an_orders_stock_puts_consumed_units_back_and_skips_the_rest()
    {
        var orderId = Guid.NewGuid();
        var stockItem = await SaveStockItemAsync(onHand: 10);
        var consumed = await SaveConsumedReservationAsync(stockItem, orderId, quantity: 3);
        var released = InventoryReservation.Create(stockItem.ProductId, orderId, Guid.NewGuid(), 1, Now);
        released.Release(Now);
        await _reservations.AddAsync(released, CancellationToken.None);

        var returned = await new ReturnOrderStockUseCase(_stockItems, _reservations, new FakeUnitOfWork(), TimeProvider.System)
            .ExecuteAsync(orderId, CancellationToken.None);

        returned.Should().Be(3);
        stockItem.QuantityOnHand.Should().Be(10);
        consumed.Status.Should().Be(ReservationStatus.Returned);
        released.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task Returning_an_orders_stock_twice_returns_it_once()
    {
        var orderId = Guid.NewGuid();
        var stockItem = await SaveStockItemAsync(onHand: 10);
        await SaveConsumedReservationAsync(stockItem, orderId, quantity: 4);
        var useCase = new ReturnOrderStockUseCase(_stockItems, _reservations, new FakeUnitOfWork(), TimeProvider.System);

        await useCase.ExecuteAsync(orderId, CancellationToken.None);
        var second = await useCase.ExecuteAsync(orderId, CancellationToken.None);

        second.Should().Be(0);
        stockItem.QuantityOnHand.Should().Be(10);
    }
}
