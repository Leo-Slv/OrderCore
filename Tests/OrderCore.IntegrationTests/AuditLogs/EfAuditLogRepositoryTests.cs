using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.AuditLogs;

/// <summary>
/// Real PostgreSQL, real migration: entries written in one context are read
/// back from another, filtered and paged newest first.
/// </summary>
public sealed class EfAuditLogRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuditLogsDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AuditLogsDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private async Task SaveAsync(params AuditLog[] entries)
    {
        await using var dbContext = CreateDbContext();
        var repository = new EfAuditLogRepository(dbContext);
        foreach (var entry in entries)
        {
            await repository.AddAsync(entry, CancellationToken.None);
        }

        await repository.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListAsync(AuditLogFilter filter, int page = 1, int pageSize = 20)
    {
        await using var dbContext = CreateDbContext();
        return await new EfAuditLogRepository(dbContext).ListPagedAsync(filter, page, pageSize, CancellationToken.None);
    }

    [Fact]
    public async Task An_entry_round_trips_with_every_field()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var entry = AuditLog.Create(userId, "OrderCancelled", "Order", orderId, """{"reason":"customer asked"}""", Start);
        await SaveAsync(entry);

        var (items, total) = await ListAsync(AuditLogFilter.None);

        total.Should().Be(1);
        var stored = items.Single();
        stored.Id.Should().Be(entry.Id);
        stored.UserId.Should().Be(userId);
        stored.Action.Should().Be("OrderCancelled");
        stored.EntityName.Should().Be("Order");
        stored.EntityId.Should().Be(orderId);
        stored.MetadataJson.Should().Be("""{"reason":"customer asked"}""");
        stored.CreatedAt.Should().Be(Start);
    }

    [Fact]
    public async Task Filtering_by_entity_returns_its_timeline_newest_first()
    {
        var orderId = Guid.NewGuid();
        await SaveAsync(
            AuditLog.Create(null, "OrderCreated", "Order", orderId, null, Start),
            AuditLog.Create(null, "OrderConfirmed", "Order", orderId, null, Start.AddMinutes(2)),
            AuditLog.Create(null, "OrderCreated", "Order", Guid.NewGuid(), null, Start.AddMinutes(1)),
            AuditLog.Create(null, "PaymentAuthorized", "Payment", orderId, null, Start.AddMinutes(1)));

        var (items, total) = await ListAsync(new AuditLogFilter("Order", orderId, null, null));

        total.Should().Be(2);
        items.Select(a => a.Action).Should().Equal("OrderConfirmed", "OrderCreated");
    }

    [Fact]
    public async Task Filtering_by_actor_and_action_combines_both()
    {
        var admin = Guid.NewGuid();
        await SaveAsync(
            AuditLog.Create(admin, "ProductPublished", "Product", Guid.NewGuid(), null, Start),
            AuditLog.Create(admin, "ProductCreated", "Product", Guid.NewGuid(), null, Start),
            AuditLog.Create(Guid.NewGuid(), "ProductPublished", "Product", Guid.NewGuid(), null, Start));

        var (items, total) = await ListAsync(new AuditLogFilter(null, null, admin, "ProductPublished"));

        total.Should().Be(1);
        items.Single().UserId.Should().Be(admin);
    }

    [Fact]
    public async Task Pages_are_cut_after_ordering_and_the_total_counts_every_match()
    {
        var entries = Enumerable.Range(0, 5)
            .Select(i => AuditLog.Create(null, "OrderCreated", "Order", Guid.NewGuid(), null, Start.AddMinutes(i)))
            .ToArray();
        await SaveAsync(entries);

        var (items, total) = await ListAsync(AuditLogFilter.None, page: 2, pageSize: 2);

        total.Should().Be(5);
        items.Select(a => a.Id).Should().Equal(entries[2].Id, entries[1].Id);
    }
}
