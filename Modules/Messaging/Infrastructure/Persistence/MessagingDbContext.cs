using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;

public sealed class MessagingDbContext : DbContext
{
    public MessagingDbContext(DbContextOptions<MessagingDbContext> options) : base(options)
    {
    }

    public DbSet<FailedMessagePersistenceModel> FailedMessages => Set<FailedMessagePersistenceModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly, type =>
            type.Namespace == "OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Configurations");
    }
}
