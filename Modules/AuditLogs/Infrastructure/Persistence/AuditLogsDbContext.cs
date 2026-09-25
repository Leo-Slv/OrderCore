using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;

public sealed class AuditLogsDbContext : DbContext
{
    public AuditLogsDbContext(DbContextOptions<AuditLogsDbContext> options) : base(options)
    {
    }

    public DbSet<AuditLogPersistenceModel> AuditLogs => Set<AuditLogPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditLogsDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Configurations");
    }
}
