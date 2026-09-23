using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create a <see cref="CatalogDbContext"/> at design time
/// — see <c>CustomersDbContextFactory</c>'s remarks on why building the
/// full Program.cs host isn't an option here.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
