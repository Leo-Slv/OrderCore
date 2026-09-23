using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovementPersistenceModel>
{
    public void Configure(EntityTypeBuilder<StockMovementPersistenceModel> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovementType).HasMaxLength(30).IsRequired();
        builder.Property(m => m.ReferenceType).HasMaxLength(100);

        builder.HasIndex(m => m.ProductId);
    }
}
