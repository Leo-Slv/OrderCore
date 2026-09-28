using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Inventory;
using OrderCore.Api.Modules.Inventory.Infrastructure.Messaging;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Orders.Infrastructure.Messaging;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments;
using OrderCore.Api.Modules.Payments.Infrastructure.Messaging;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.IntegrationTests;

/// <summary>
/// The modules' outboxes wired by hand, for tests that build repositories
/// and use cases without the host. The contracts come from the modules'
/// own registrations, so a test outbox knows exactly what the API does.
/// </summary>
public static class TestOutboxes
{
    private static readonly IntegrationEventRegistry Registry = CreateRegistry();

    public static PaymentsOutbox Payments(PaymentsDbContext dbContext) => new(Writer(dbContext));

    public static OrdersOutbox Orders(OrdersDbContext dbContext) => new(Writer(dbContext));

    public static InventoryOutbox Inventory(InventoryDbContext dbContext) => new(Writer(dbContext));

    private static OutboxWriter<TDbContext> Writer<TDbContext>(TDbContext dbContext)
        where TDbContext : Microsoft.EntityFrameworkCore.DbContext =>
        new(dbContext, Registry, new MessageContext());

    private static IntegrationEventRegistry CreateRegistry()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection()
            .AddPaymentsModule(configuration)
            .AddOrdersModule(configuration)
            .AddInventoryModule(configuration);
        return services.IntegrationEventRegistry();
    }
}
