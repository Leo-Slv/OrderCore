using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Owns only the Catalog module's own tables — see
/// <c>CustomersDbContext</c>'s remarks on why each module gets its own
/// DbContext instead of one project-wide context.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<ProductPersistenceModel> Products => Set<ProductPersistenceModel>();

    public DbSet<CategoryPersistenceModel> Categories => Set<CategoryPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Configurations");
    }
}
