using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderTimelineEntryConfiguration : IEntityTypeConfiguration<OrderTimelineEntryPersistenceModel>
{
    public void Configure(EntityTypeBuilder<OrderTimelineEntryPersistenceModel> builder)
    {
        builder.ToTable("order_timeline");

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).ValueGeneratedNever();

        builder.Property(e => e.Sequence).UseIdentityByDefaultColumn();
        builder.Property(e => e.Type).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(50).IsRequired();
        builder.Property(e => e.DetailsJson).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(e => new { e.OrderId, e.OccurredAt });
    }
}
