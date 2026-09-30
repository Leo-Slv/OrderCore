using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// <c>dotnet OrderCore.Api.dll migrate</c> — the deployment step that applies
/// every module's migrations (Docs/operations/deployment.md) — run through
/// the API's real entry point against an empty database: it migrates all
/// eight contexts with nothing but a connection string (no JWT key, no
/// broker), running it again changes nothing, and a database it can't reach
/// is a non-zero exit.
/// </summary>
public sealed class MigrateCommandTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    /// <summary>The API's entry point, as <c>dotnet OrderCore.Api.dll migrate ...</c> runs it.</summary>
    private static int Migrate(string connectionString) =>
        (int)typeof(Program).Assembly.EntryPoint!.Invoke(
            null, [new[] { "migrate", $"--ConnectionStrings:OrderCoreDb={connectionString}" }])!;

    private DbContextOptions<T> Options<T>()
        where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(_postgres.GetConnectionString()).Options;

    private async Task<IReadOnlyList<string>> PendingAsync()
    {
        DbContext[] contexts =
        [
            new IdentityDbContext(Options<IdentityDbContext>()),
            new CustomersDbContext(Options<CustomersDbContext>()),
            new CatalogDbContext(Options<CatalogDbContext>()),
            new InventoryDbContext(Options<InventoryDbContext>()),
            new OrdersDbContext(Options<OrdersDbContext>()),
            new PaymentsDbContext(Options<PaymentsDbContext>()),
            new AuditLogsDbContext(Options<AuditLogsDbContext>()),
            new MessagingDbContext(Options<MessagingDbContext>()),
        ];

        var pending = new List<string>();
        foreach (var context in contexts)
        {
            await using (context)
            {
                pending.AddRange((await context.Database.GetPendingMigrationsAsync()).Select(m => $"{context.GetType().Name}: {m}"));
            }
        }

        return pending;
    }

    [Fact]
    public async Task Migrate_brings_every_module_database_up_to_date_and_is_safe_to_repeat()
    {
        (await PendingAsync()).Should().NotBeEmpty("the database starts empty");

        Migrate(_postgres.GetConnectionString()).Should().Be(0);

        (await PendingAsync()).Should().BeEmpty();
        Migrate(_postgres.GetConnectionString()).Should().Be(0, "nothing is pending the second time");
    }

    [Fact]
    public void Migrate_fails_with_a_non_zero_exit_when_the_database_is_unreachable()
    {
        var unreachable = "Host=127.0.0.1;Port=1;Database=ordercore;Username=ordercore;Password=ordercore;Timeout=2";

        Migrate(unreachable).Should().Be(1);
    }
}
