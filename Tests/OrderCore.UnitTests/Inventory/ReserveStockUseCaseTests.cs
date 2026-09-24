using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using Xunit;

namespace OrderCore.UnitTests.Inventory;

public sealed class ReserveStockUseCaseTests
{
    private static ReserveStockCommand Command(Guid productId, int quantity = 1) =>
        new(productId, Guid.NewGuid(), Guid.NewGuid(), quantity);

    [Fact]
    public async Task ExecuteAsync_succeeds_when_stock_is_available()
    {
        var products = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 1, null, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var useCase = new ReserveStockUseCase(
            products, new FakeInventoryReservationRepository(), new FakeUnitOfWork(), new FakeAuditLogService(), TimeProvider.System);

        var result = await useCase.ExecuteAsync(Command(stockItem.ProductId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.ReservationId.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_fails_without_retrying_when_stock_is_genuinely_insufficient()
    {
        var products = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 0, null, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var unitOfWork = new FakeUnitOfWork();
        var useCase = new ReserveStockUseCase(products, new FakeInventoryReservationRepository(), unitOfWork, new FakeAuditLogService(), TimeProvider.System);

        var result = await useCase.ExecuteAsync(Command(stockItem.ProductId), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        unitOfWork.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_retries_and_succeeds_after_a_concurrency_conflict()
    {
        var products = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var unitOfWork = FakeUnitOfWork.ThatConflictsThenSucceeds(conflictCount: 2);
        var useCase = new ReserveStockUseCase(products, new FakeInventoryReservationRepository(), unitOfWork, new FakeAuditLogService(), TimeProvider.System);

        var result = await useCase.ExecuteAsync(Command(stockItem.ProductId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        unitOfWork.SaveChangesCallCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_gives_up_after_the_max_retry_count()
    {
        var products = new FakeStockItemRepository();
        var stockItem = StockItem.Create(Guid.NewGuid(), 5, null, DateTimeOffset.UtcNow);
        await products.AddAsync(stockItem, CancellationToken.None);
        var unitOfWork = FakeUnitOfWork.ThatConflictsThenSucceeds(conflictCount: 3);
        var useCase = new ReserveStockUseCase(products, new FakeInventoryReservationRepository(), unitOfWork, new FakeAuditLogService(), TimeProvider.System);

        var act = () => useCase.ExecuteAsync(Command(stockItem.ProductId), CancellationToken.None);

        await act.Should().ThrowAsync<StockConcurrencyConflictException>();
        unitOfWork.SaveChangesCallCount.Should().Be(3);
    }
}
