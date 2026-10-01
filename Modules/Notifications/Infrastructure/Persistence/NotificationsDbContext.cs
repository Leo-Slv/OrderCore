using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Infrastructure.Messaging;

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

        // Which of Orders' events the order e-mails already handled (Docs/specs/events).
        modelBuilder.AddInbox("notifications");
    }
}
