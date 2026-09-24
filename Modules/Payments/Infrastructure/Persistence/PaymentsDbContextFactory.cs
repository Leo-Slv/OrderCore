using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create a <see cref="PaymentsDbContext"/> at design time
/// — see <c>CustomersDbContextFactory</c>'s remarks.
/// </summary>
public sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PaymentsDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new PaymentsDbContext(optionsBuilder.Options);
    }
}
