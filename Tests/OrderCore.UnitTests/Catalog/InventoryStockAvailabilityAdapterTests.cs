using FluentAssertions;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Infrastructure.Adapters;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.UnitTests.Inventory;
using Xunit;

namespace OrderCore.UnitTests.Catalog;

/// <summary>
/// Runs Inventory's real <see cref="GetStockAvailabilityUseCase"/> over its
/// in-memory repository, so the quantity-to-state mapping is checked
/// against what Inventory actually reports, in both directions (the
/// storefront's availability and the backoffice's stock levels).
/// </summary>
public sealed class InventoryStockAvailabilityAdapterTests
{
    private static InventoryStockAvailabilityAdapter CreateAdapter(FakeStockItemRepository stockItems) =>
        new(
            new GetStockAvailabilityUseCase(stockItems),
            new EnsureStockItemUseCase(stockItems, new FakeUnitOfWork(), TimeProvider.System),
            new GetStockLevelsUseCase(stockItems),
            new ListProductIdsInStockStateUseCase(stockItems));

    [Fact]
    public async Task GetAvailabilityAsync_maps_stock_to_a_state_for_every_requested_product()
    {
        var stockItems = new FakeStockItemRepository();
        var inStock = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        var fullyReserved = StockItem.Create(Guid.NewGuid(), 2, null, DateTimeOffset.UtcNow);
        fullyReserved.TryReserve(2);
        await stockItems.AddAsync(inStock, CancellationToken.None);
        await stockItems.AddAsync(fullyReserved, CancellationToken.None);
        var withoutStockRecord = Guid.NewGuid();

        var adapter = CreateAdapter(stockItems);
        var result = await adapter.GetAvailabilityAsync(
            [inStock.ProductId, fullyReserved.ProductId, withoutStockRecord], CancellationToken.None);

        result.Should().BeEquivalentTo(new Dictionary<Guid, StockAvailability>
        {
            [inStock.ProductId] = StockAvailability.InStock,
            [fullyReserved.ProductId] = StockAvailability.OutOfStock,
            [withoutStockRecord] = StockAvailability.OutOfStock,
        });
    }

    [Fact]
    public async Task Stock_levels_carry_the_numbers_and_a_state_and_filter_by_state()
    {
        var stockItems = new FakeStockItemRepository();
        var low = StockItem.Create(Guid.NewGuid(), 3, null, DateTimeOffset.UtcNow);
        low.SetReorderLevel(3, DateTimeOffset.UtcNow);
        low.TryReserve(1);
        var plenty = StockItem.Create(Guid.NewGuid(), 50, null, DateTimeOffset.UtcNow);
        await stockItems.AddAsync(low, CancellationToken.None);
        await stockItems.AddAsync(plenty, CancellationToken.None);
        var adapter = CreateAdapter(stockItems);

        var levels = await adapter.GetStockLevelsAsync([low.ProductId, plenty.ProductId, Guid.NewGuid()], CancellationToken.None);

        levels.Should().HaveCount(2);
        levels[low.ProductId].Should().Be(new ProductStockLevel(3, 1, 2, 3, StockAvailability.LowStock));
        levels[plenty.ProductId].State.Should().Be(StockAvailability.InStock);
        (await adapter.ListProductIdsInStateAsync(StockAvailability.LowStock, CancellationToken.None))
            .Should().Equal(low.ProductId);
    }

    [Fact]
    public async Task Ensuring_a_stock_record_creates_an_empty_one_in_inventory()
    {
        var stockItems = new FakeStockItemRepository();
        var productId = Guid.NewGuid();

        await CreateAdapter(stockItems).EnsureStockRecordAsync(productId, CancellationToken.None);

        (await stockItems.GetByProductIdAsync(productId, CancellationToken.None))!.QuantityOnHand.Should().Be(0);
    }
}
