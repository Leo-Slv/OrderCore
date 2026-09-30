using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    public DbSet<EmailMessagePersistenceModel> Emails => Set<EmailMessagePersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Configurations");
    }
}
