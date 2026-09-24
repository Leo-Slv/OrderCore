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
/// against what Inventory actually reports. The <c>LowStock</c> case needs
/// a reorder level, which only persisted data can have today; it is
/// covered by <c>EfStockItemRepositoryTests</c> on the Inventory side.
/// </summary>
public sealed class InventoryStockAvailabilityAdapterTests
{
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

        var adapter = new InventoryStockAvailabilityAdapter(new GetStockAvailabilityUseCase(stockItems));
        var result = await adapter.GetAvailabilityAsync(
            [inStock.ProductId, fullyReserved.ProductId, withoutStockRecord], CancellationToken.None);

        result.Should().BeEquivalentTo(new Dictionary<Guid, StockAvailability>
        {
            [inStock.ProductId] = StockAvailability.InStock,
            [fullyReserved.ProductId] = StockAvailability.OutOfStock,
            [withoutStockRecord] = StockAvailability.OutOfStock,
        });
    }
}
