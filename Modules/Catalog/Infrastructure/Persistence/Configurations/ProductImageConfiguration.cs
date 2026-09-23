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

        builder.Property(i => i.Url).HasMaxLength(2000).IsRequired();
        builder.Property(i => i.AltText).HasMaxLength(200);

        builder.HasIndex(i => i.ProductId);
    }
}
