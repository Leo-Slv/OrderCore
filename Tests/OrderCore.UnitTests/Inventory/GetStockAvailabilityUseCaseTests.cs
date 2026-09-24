using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class GetStockAvailabilityUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ExecuteAsync_reports_available_quantity_net_of_reservations()
    {
        var repository = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 10, null, Now);
        stockItem.TryReserve(4);
        await repository.AddAsync(stockItem, CancellationToken.None);

        var result = await new GetStockAvailabilityUseCase(repository).ExecuteAsync([stockItem.ProductId], CancellationToken.None);

        result.Should().ContainSingle().Which.QuantityAvailable.Should().Be(6);
    }

    [Fact]
    public async Task ExecuteAsync_reports_a_product_without_stock_record_as_zero_available()
    {
        var productId = Guid.NewGuid();

        var result = await new GetStockAvailabilityUseCase(new FakeStockItemRepository()).ExecuteAsync([productId], CancellationToken.None);

        var availability = result.Should().ContainSingle().Subject;
        availability.ProductId.Should().Be(productId);
        availability.QuantityAvailable.Should().Be(0);
        availability.IsLowStock.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_returns_one_entry_per_distinct_product()
    {
        var repository = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, Now);
        await repository.AddAsync(stockItem, CancellationToken.None);
        var missingProductId = Guid.NewGuid();

        var result = await new GetStockAvailabilityUseCase(repository)
            .ExecuteAsync([stockItem.ProductId, missingProductId, stockItem.ProductId], CancellationToken.None);

        result.Select(r => r.ProductId).Should().BeEquivalentTo([stockItem.ProductId, missingProductId]);
    }

    [Fact]
    public async Task ExecuteAsync_with_no_products_returns_empty()
    {
        var result = await new GetStockAvailabilityUseCase(new FakeStockItemRepository()).ExecuteAsync([], CancellationToken.None);

        result.Should().BeEmpty();
    }
}
