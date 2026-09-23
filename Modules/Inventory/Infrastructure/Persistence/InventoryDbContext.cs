using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Owns only the Inventory module's own tables — see
/// <c>CustomersDbContext</c>'s remarks on why each module gets its own
/// DbContext instead of one project-wide context.
/// </summary>
public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<StockItemPersistenceModel> StockItems => Set<StockItemPersistenceModel>();

    public DbSet<InventoryReservationPersistenceModel> Reservations => Set<InventoryReservationPersistenceModel>();

    public DbSet<StockMovementPersistenceModel> StockMovements => Set<StockMovementPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Configurations");
    }
}
