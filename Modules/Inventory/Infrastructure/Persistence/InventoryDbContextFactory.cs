using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create an <see cref="InventoryDbContext"/> at design
/// time — see <c>CustomersDbContextFactory</c>'s remarks.
/// </summary>
public sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new InventoryDbContext(optionsBuilder.Options);
    }
}
