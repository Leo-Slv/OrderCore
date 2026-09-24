using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Payments.Infrastructure.Outbox;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();

        builder.HasIndex(m => m.ProcessedAt);
    }
}
