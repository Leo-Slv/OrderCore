using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Application.UseCases;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Inventory;

/// <summary>
/// Same shape as EfCustomerRepositoryTests/EfProductRepositoryTests
/// (Testcontainers.PostgreSql, real migration), plus the concurrency test
/// from section 34 of the project context: `Stock = 1`, N concurrent
/// reservation requests, exactly 1 succeeds — the scenario this whole
/// feature exists to make possible.
/// </summary>
public sealed class EfStockItemRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var dbContext = new InventoryDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private InventoryDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    /// <summary>
    /// No domain events are raised in this scenario worth reacting to
    /// beyond what the test itself asserts, so a dispatcher that does
    /// nothing is enough here — StockMovementRecorder's own behavior is
    /// covered separately.
    /// </summary>
    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(
            string action,
            string entityName,
            Guid? entityId,
            IReadOnlyDictionary<string, string?>? metadata,
            Guid? userId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static ReserveStockUseCase CreateReserveStockUseCase(InventoryDbContext dbContext)
    {
        var stockItemRepository = new EfStockItemRepository(dbContext);
        var reservationRepository = new EfInventoryReservationRepository(dbContext);
        var unitOfWork = new InventoryUnitOfWork(dbContext, stockItemRepository, reservationRepository, new NoOpDomainEventDispatcher());

        return new ReserveStockUseCase(stockItemRepository, reservationRepository, unitOfWork, new NoOpAuditLogService(), TimeProvider.System);
    }

    [Fact]
    public async Task AddAsync_then_GetByProductIdAsync_round_trips_a_stock_item()
    {
        var productId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfStockItemRepository(dbContext);
            var reservationRepository = new EfInventoryReservationRepository(dbContext);
            var unitOfWork = new InventoryUnitOfWork(dbContext, repository, reservationRepository, new NoOpDomainEventDispatcher());
            var stockItem = StockItem.Create(productId, 10, null, DateTimeOffset.UtcNow);
            await repository.AddAsync(stockItem, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfStockItemRepository(dbContext);
            var reloaded = await repository.GetByProductIdAsync(productId, CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.QuantityOnHand.Should().Be(10);
        }
    }

    [Fact]
    public async Task ListByProductIdsAsync_returns_only_the_requested_products_with_low_stock_flag()
    {
        var lowStockProductId = Guid.NewGuid();
        var plentyProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        // Written as persistence models directly: nothing in the domain can
        // set ReorderLevel yet, and this is exactly the shape a row with one
        // would have once a reorder-level feature exists.
        await using (var dbContext = CreateDbContext())
        {
            dbContext.StockItems.AddRange(
                StockRow(lowStockProductId, quantityOnHand: 3, reorderLevel: 5),
                StockRow(plentyProductId, quantityOnHand: 50, reorderLevel: 5),
                StockRow(otherProductId, quantityOnHand: 1, reorderLevel: 0));
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = CreateDbContext())
        {
            var result = await new EfStockItemRepository(dbContext)
                .ListByProductIdsAsync([lowStockProductId, plentyProductId, Guid.NewGuid()], CancellationToken.None);

            result.Should().HaveCount(2);
            result.Single(s => s.ProductId == lowStockProductId).IsLowStock.Should().BeTrue();
            result.Single(s => s.ProductId == plentyProductId).IsLowStock.Should().BeFalse();
            dbContext.ChangeTracker.Entries().Should().BeEmpty();
        }
    }

    private static StockItemPersistenceModel StockRow(Guid productId, int quantityOnHand, int reorderLevel) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        QuantityOnHand = quantityOnHand,
        ReorderLevel = reorderLevel,
        UpdatedAt = DateTimeOffset.UtcNow,
        Version = 1,
    };

    [Fact]
    public async Task Stock_of_one_under_concurrent_reservation_requests_lets_exactly_one_succeed()
    {
        var productId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfStockItemRepository(dbContext);
            var reservationRepository = new EfInventoryReservationRepository(dbContext);
            var unitOfWork = new InventoryUnitOfWork(dbContext, repository, reservationRepository, new NoOpDomainEventDispatcher());
            await repository.AddAsync(StockItem.Create(productId, 1, null, DateTimeOffset.UtcNow), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        const int concurrentRequests = 20;
        var orderId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            // Each simulated request gets its own DbContext, the same way
            // each real HTTP request would get its own Scoped instance.
            await using var dbContext = CreateDbContext();
            var useCase = CreateReserveStockUseCase(dbContext);
            var command = new ReserveStockCommand(productId, orderId, Guid.NewGuid(), Quantity: 1);
            return await useCase.ExecuteAsync(command, CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        results.Count(r => r.Succeeded).Should().Be(1);
        results.Count(r => !r.Succeeded).Should().Be(concurrentRequests - 1);

        await using var verifyDbContext = CreateDbContext();
        var finalStockItem = await new EfStockItemRepository(verifyDbContext).GetByProductIdAsync(productId, CancellationToken.None);
        finalStockItem!.QuantityAvailable.Should().Be(0);
    }
}
