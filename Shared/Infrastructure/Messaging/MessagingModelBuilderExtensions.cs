using Microsoft.EntityFrameworkCore;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// Maps the outbox and the inbox into a module's own <c>DbContext</c>. Each
/// module gets its own tables (all modules share one database, so the
/// module's name is part of the table name): the outbox has to live next
/// to the aggregates it records events for, and the inbox next to the
/// changes of the handler that consumed the message, for each to commit
/// in the same transaction.
/// </summary>
public static class MessagingModelBuilderExtensions
{
    /// <summary>Maps <see cref="OutboxMessage"/> to <c>{module}_outbox_messages</c>.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder, string module)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable($"{module}_outbox_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
            builder.Property(m => m.PayloadJson).IsRequired();
            builder.Property(m => m.TraceParent).HasMaxLength(100);
            builder.Property(m => m.TraceState).HasMaxLength(512);
            builder.Property(m => m.LastError).HasMaxLength(2000);

            // The relay only ever reads what is still waiting, oldest first.
            builder.HasIndex(m => m.OccurredAt).HasFilter("\"SentAt\" IS NULL");
        });

        return modelBuilder;
    }

    /// <summary>Maps <see cref="InboxMessage"/> to <c>{module}_processed_messages</c>.</summary>
    public static ModelBuilder AddInbox(this ModelBuilder modelBuilder, string module)
    {
        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable($"{module}_processed_messages");
            builder.HasKey(m => new { m.MessageId, m.Consumer });
            builder.Property(m => m.Consumer).HasMaxLength(200);
        });

        return modelBuilder;
    }
}
