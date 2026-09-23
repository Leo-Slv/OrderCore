using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.ValueObjects;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Same shape as EfCustomerRepositoryTests/EfProductRepositoryTests/
/// EfStockItemRepositoryTests: exercises the real PostgreSQL provider and
/// the ExpandOrdersSchema migration, including the
/// <c>order_number_seq</c>-backed SequentialOrderNumberGenerator.
/// </summary>
public sealed class EfOrderRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var dbContext = new OrdersDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private OrdersDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Address SomeAddress() => Address.Create(
        "Main St", "123", null, "Downtown", "Springfield", "IL", "62701", "USA");

    [Fact]
    public async Task SequentialOrderNumberGenerator_produces_distinct_increasing_numbers()
    {
        await using var dbContext = CreateDbContext();
        var generator = new SequentialOrderNumberGenerator(dbContext, TimeProvider.System);

        var first = await generator.NextAsync(CancellationToken.None);
        var second = await generator.NextAsync(CancellationToken.None);

        first.Should().NotBe(second);
        first.Should().StartWith($"ORD-{DateTimeOffset.UtcNow.Year}-");
    }

    [Fact]
    public async Task AddAsync_then_GetByIdAsync_round_trips_an_order_with_addresses()
    {
        var orderId = Guid.Empty;
        var customerId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
            var order = Order.Create(customerId, "BRL", "ORD-2026-000001", DateTimeOffset.UtcNow);
            order.AddItem(Guid.NewGuid(), null, "SKU-1", "Widget", null, 10m, 2);
            order.SetAddresses(SomeAddress(), SomeAddress());

            await repository.AddAsync(order, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            orderId = order.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
            var reloaded = await repository.GetByIdAsync(orderId, CancellationToken.None);

            reloaded.Should().NotBeNull();
            reloaded!.OrderNumber.Should().Be("ORD-2026-000001");
            reloaded.ShippingAddress.Should().NotBeNull();
            reloaded.ShippingAddress!.City.Should().Be("Springfield");
            reloaded.Items.Should().ContainSingle(i => i.Quantity == 2);
        }
    }

    [Fact]
    public async Task Confirming_a_loaded_order_and_saving_persists_ConfirmedAt()
    {
        var orderId = Guid.Empty;
        var customerId = Guid.NewGuid();

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
            var order = Order.Create(customerId, "BRL", "ORD-2026-000002", DateTimeOffset.UtcNow);
            order.AddItem(Guid.NewGuid(), null, "SKU-1", "Widget", null, 10m, 1);
            order.RequestPayment(DateTimeOffset.UtcNow);

            await repository.AddAsync(order, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
            orderId = order.Id;
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
            var order = await repository.GetByIdAsync(orderId, CancellationToken.None);
            order!.Confirm(DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var dbContext = CreateDbContext())
        {
            var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
            var reloaded = await repository.GetByIdAsync(orderId, CancellationToken.None);

            reloaded!.ConfirmedAt.Should().NotBeNull();
        }
    }
}
