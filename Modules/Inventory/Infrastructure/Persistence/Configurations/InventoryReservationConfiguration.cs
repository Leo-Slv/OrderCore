using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservationPersistenceModel>
{
    public void Configure(EntityTypeBuilder<InventoryReservationPersistenceModel> builder)
    {
        builder.ToTable("inventory_reservations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.ProductId);

        builder.Property(r => r.Version).IsConcurrencyToken();
    }
}
