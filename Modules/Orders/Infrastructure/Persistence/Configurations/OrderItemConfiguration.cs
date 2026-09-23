using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItemPersistenceModel>
{
    public void Configure(EntityTypeBuilder<OrderItemPersistenceModel> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => new { i.OrderId, i.ProductId });

        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
    }
}
