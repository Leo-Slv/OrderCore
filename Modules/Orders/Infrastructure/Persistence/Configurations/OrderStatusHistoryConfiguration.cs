using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistoryPersistenceModel>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistoryPersistenceModel> builder)
    {
        builder.ToTable("order_status_history");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Sequence).UseIdentityByDefaultColumn();

        builder.Property(h => h.FromStatus).HasMaxLength(20);
        builder.Property(h => h.ToStatus).HasMaxLength(20).IsRequired();

        builder.HasIndex(h => h.OrderId);
    }
}
