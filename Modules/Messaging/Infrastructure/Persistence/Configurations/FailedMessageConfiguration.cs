using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Configurations;

public sealed class FailedMessageConfiguration : IEntityTypeConfiguration<FailedMessagePersistenceModel>
{
    public void Configure(EntityTypeBuilder<FailedMessagePersistenceModel> builder)
    {
        builder.ToTable("failed_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Consumer).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.TraceParent).HasMaxLength(100);
        builder.Property(m => m.TraceState).HasMaxLength(512);
        builder.Property(m => m.LastError).HasMaxLength(FailedMessage.MaxErrorLength).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Version).IsConcurrencyToken();

        // The backoffice lists the pending ones, most recent failure first.
        builder.HasIndex(m => new { m.Status, m.LastFailedAt });
        builder.HasIndex(m => m.MessageId);
    }
}
