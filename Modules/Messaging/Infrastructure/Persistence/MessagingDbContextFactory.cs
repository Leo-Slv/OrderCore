using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new MessagingDbContext(optionsBuilder.Options);
    }
}
