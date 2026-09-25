using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The backoffice queries of <see cref="EfOrderRepository"/> against real
/// PostgreSQL: the admin list's filters and paging, the count by status,
/// and the revenue sum, whose per-order total is computed in SQL from the
/// items (the total isn't stored).
/// </summary>
public sealed class OrderBackofficeQueriesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrderStatus[] RevenueStatuses =
        [OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private OrdersDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Two lines (2 × 50 and 1 × 30 with 5 off) plus 10 shipping: a total of 135.
    /// Confirmed at <paramref name="createdAt"/> + 1 minute when it gets that far.
    /// </summary>
    private async Task<Order> SaveOrderAsync(
        OrderStatus status, DateTimeOffset createdAt, Guid? customerId = null, string currency = "BRL")
    {
        var order = Order.Create(customerId ?? Guid.NewGuid(), currency, $"ORD-{Guid.NewGuid():N}"[..16], createdAt);
        var discounted = Guid.NewGuid();
        order.AddItem(Guid.NewGuid(), null, "SKU-A", "Lamp", null, 50m, 2);
        order.AddItem(discounted, null, "SKU-B", "Bulb", null, 30m, 1);
        order.ApplyItemDiscount(discounted, 5m);
        order.SetShippingAmount(10m);

        if (status != OrderStatus.Created)
        {
            order.RequestPayment(createdAt);
        }

        if (status == OrderStatus.Cancelled)
        {
            order.Cancel("changed mind", createdAt);
        }

        if (status is OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.Confirm(createdAt.AddMinutes(1));
        }

        if (status is OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.StartProcessing(createdAt);
        }

        if (status is OrderStatus.Shipped or OrderStatus.Delivered)
        {
            order.Ship(createdAt);
        }

        if (status == OrderStatus.Delivered)
        {
            order.Deliver(createdAt);
        }

        await using var dbContext = CreateDbContext();
        var repository = new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher());
        await repository.AddAsync(order, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        order.TotalAmount.Should().Be(135m);
        return order;
    }

    private async Task<T> QueryAsync<T>(Func<EfOrderRepository, Task<T>> query)
    {
        await using var dbContext = CreateDbContext();
        return await query(new EfOrderRepository(dbContext, new NoOpDomainEventDispatcher()));
    }

    [Fact]
    public async Task The_admin_list_filters_by_status_customer_and_creation_range_newest_first()
    {
        var customerId = Guid.NewGuid();
        var oldest = await SaveOrderAsync(OrderStatus.Confirmed, Start, customerId);
        var middle = await SaveOrderAsync(OrderStatus.Confirmed, Start.AddDays(1), customerId);
        await SaveOrderAsync(OrderStatus.Cancelled, Start.AddDays(2), customerId);
        await SaveOrderAsync(OrderStatus.Confirmed, Start.AddDays(3));

        var (confirmedOfCustomer, total) = await QueryAsync(r => r.ListAsync(
            new ListOrdersFilter { Status = OrderStatus.Confirmed, CustomerId = customerId }, CancellationToken.None));
        total.Should().Be(2);
        confirmedOfCustomer.Select(o => o.Id).Should().Equal(middle.Id, oldest.Id);
        confirmedOfCustomer.Should().OnlyContain(o => o.Items.Count == 2);

        var (inRange, rangeTotal) = await QueryAsync(r => r.ListAsync(
            new ListOrdersFilter { CreatedFrom = Start.AddDays(1), CreatedTo = Start.AddDays(3) }, CancellationToken.None));
        rangeTotal.Should().Be(2);
        inRange.Select(o => o.CreatedAt).Should().BeInDescendingOrder();

        var (secondPage, allTotal) = await QueryAsync(r => r.ListAsync(
            new ListOrdersFilter { Page = 2, PageSize = 3 }, CancellationToken.None));
        allTotal.Should().Be(4);
        secondPage.Should().ContainSingle().Which.Id.Should().Be(oldest.Id);
    }

    [Fact]
    public async Task Orders_created_in_the_period_are_counted_by_status_with_every_status_present()
    {
        await SaveOrderAsync(OrderStatus.Confirmed, Start);
        await SaveOrderAsync(OrderStatus.Confirmed, Start.AddHours(1));
        await SaveOrderAsync(OrderStatus.Delivered, Start.AddHours(2));
        await SaveOrderAsync(OrderStatus.Confirmed, Start.AddDays(-10));

        var counts = await QueryAsync(r => r.CountByStatusAsync(Start, Start.AddDays(1), CancellationToken.None));

        counts.Should().HaveCount(Enum.GetValues<OrderStatus>().Length);
        counts[OrderStatus.Confirmed].Should().Be(2);
        counts[OrderStatus.Delivered].Should().Be(1);
        counts[OrderStatus.Cancelled].Should().Be(0);
    }

    [Fact]
    public async Task Revenue_sums_the_computed_totals_of_orders_confirmed_in_the_period_per_currency()
    {
        await SaveOrderAsync(OrderStatus.Confirmed, Start);
        await SaveOrderAsync(OrderStatus.Shipped, Start.AddHours(1));
        await SaveOrderAsync(OrderStatus.Delivered, Start.AddHours(2), currency: "USD");
        await SaveOrderAsync(OrderStatus.Cancelled, Start.AddHours(3));
        await SaveOrderAsync(OrderStatus.PendingPayment, Start.AddHours(4));
        await SaveOrderAsync(OrderStatus.Confirmed, Start.AddDays(-10));

        var revenue = await QueryAsync(r => r.SumConfirmedTotalsAsync(Start, Start.AddDays(1), RevenueStatuses, CancellationToken.None));

        revenue.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["BRL"] = 270m, ["USD"] = 135m });
    }
}
