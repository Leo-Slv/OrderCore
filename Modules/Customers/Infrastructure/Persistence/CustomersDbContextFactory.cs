using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create a <see cref="CustomersDbContext"/> at design
/// time (migrations add/update-database) without building the full
/// Program.cs host — which today fails at design time regardless, because
/// Orders' IOrderRepository has no registered implementation yet (a known,
/// pre-existing gap, not something this factory needs to solve).
/// </summary>
public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new CustomersDbContext(optionsBuilder.Options);
    }
}
