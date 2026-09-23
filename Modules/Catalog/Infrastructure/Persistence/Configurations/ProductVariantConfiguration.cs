using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariantPersistenceModel>
{
    public void Configure(EntityTypeBuilder<ProductVariantPersistenceModel> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Sku).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.AttributesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.AdditionalPrice).HasPrecision(18, 2);

        builder.HasIndex(v => v.ProductId);
    }
}
