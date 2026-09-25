using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;

public sealed class AuditLogsDbContextFactory : IDesignTimeDbContextFactory<AuditLogsDbContext>
{
    public AuditLogsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AuditLogsDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("OrderCoreDb"));

        return new AuditLogsDbContext(optionsBuilder.Options);
    }
}
