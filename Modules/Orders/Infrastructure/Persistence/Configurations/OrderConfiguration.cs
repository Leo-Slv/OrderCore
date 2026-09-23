using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderPersistenceModel>
{
    public void Configure(EntityTypeBuilder<OrderPersistenceModel> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();

        builder.HasIndex(o => o.CustomerId);

        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
