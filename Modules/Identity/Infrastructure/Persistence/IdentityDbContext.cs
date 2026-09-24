using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<UserAccountPersistenceModel> UserAccounts => Set<UserAccountPersistenceModel>();

    public DbSet<RefreshSessionPersistenceModel> RefreshSessions => Set<RefreshSessionPersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Configurations");
    }
}
