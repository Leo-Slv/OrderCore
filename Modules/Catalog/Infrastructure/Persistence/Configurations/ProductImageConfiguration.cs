using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImagePersistenceModel>
{
    public void Configure(EntityTypeBuilder<ProductImagePersistenceModel> builder)
    {
        builder.ToTable("product_images");

        builder.HasKey(i => i.Id);

        // The id is assigned by the domain, not the database. Without this, EF
        // Core treats a new child that already has a key, found while saving
        // its parent, as an existing row and runs an UPDATE (0 rows, concurrency
        // exception) instead of an INSERT.
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Url).HasMaxLength(2000).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(200);

        builder.HasIndex(i => i.ProductId);
    }
}
