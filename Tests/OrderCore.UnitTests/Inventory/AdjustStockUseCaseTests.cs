using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class AdjustStockUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_applies_the_adjustment()
    {
        var products = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var useCase = new AdjustStockUseCase(products, new FakeUnitOfWork(), TimeProvider.System);

        var output = await useCase.ExecuteAsync(stockItem.ProductId, -2, "damaged goods", CancellationToken.None);

        output.QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_for_unknown_product_throws()
    {
        var useCase = new AdjustStockUseCase(new FakeStockItemRepository(), new FakeUnitOfWork(), TimeProvider.System);

        var act = () => useCase.ExecuteAsync(Guid.NewGuid(), 1, "reason", CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "stock_item_not_found");
    }
}
