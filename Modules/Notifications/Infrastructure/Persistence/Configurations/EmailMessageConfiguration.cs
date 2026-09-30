using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessagePersistenceModel>
{
    public void Configure(EntityTypeBuilder<EmailMessagePersistenceModel> builder)
    {
        builder.ToTable("notification_emails");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.To).HasMaxLength(320).IsRequired();
        builder.Property(m => m.Template).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(500).IsRequired();
        builder.Property(m => m.HtmlBody).IsRequired();
        builder.Property(m => m.TextBody).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(20).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(EmailMessage.MaxErrorLength);
        builder.Property(m => m.Version).IsConcurrencyToken();

        // The dispatcher lists the pending ones that are due.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });

        // Retention removes messages sent or given up on long ago.
        builder.HasIndex(m => m.SentAt).HasFilter("\"SentAt\" IS NOT NULL");
        builder.HasIndex(m => m.FailedAt).HasFilter("\"FailedAt\" IS NOT NULL");
    }
}
