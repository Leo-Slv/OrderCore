using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence;

/// <summary>
/// Owns only the Orders module's own tables — see
/// <c>CustomersDbContext</c>'s remarks on why each module gets its own
/// DbContext instead of one project-wide context.
/// </summary>
public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    public DbSet<OrderPersistenceModel> Orders => Set<OrderPersistenceModel>();

    public DbSet<OrderStatusHistoryPersistenceModel> StatusHistory => Set<OrderStatusHistoryPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Configurations");

        // Backs SequentialOrderNumberGenerator — a PostgreSQL sequence, not
        // an application-level counter, so concurrent order creation can
        // never hand out the same number twice.
        modelBuilder.HasSequence<long>("order_number_seq").StartsAt(1).IncrementsBy(1);
    }
}
