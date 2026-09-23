using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence;

/// <summary>
/// Owns only the Customers module's own tables, the same way
/// <c>CustomersDbContext</c> is scoped in 02-customers.md — a modular
/// monolith module owns its slice of the schema instead of a single
/// project-wide DbContext, so module boundaries hold even at the
/// persistence layer.
/// </summary>
public sealed class CustomersDbContext : DbContext
{
    public CustomersDbContext(DbContextOptions<CustomersDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerPersistenceModel> Customers => Set<CustomerPersistenceModel>();

    public DbSet<CustomerAddressPersistenceModel> CustomerAddresses => Set<CustomerAddressPersistenceModel>();

    public DbSet<CustomerPaymentMethodPersistenceModel> CustomerPaymentMethods => Set<CustomerPaymentMethodPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Configurations");
    }
}
