using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryPersistenceModel>
{
    public void Configure(EntityTypeBuilder<CategoryPersistenceModel> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();

        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Version).IsConcurrencyToken();

        // Self-referencing: Restrict rather than Cascade, since a
        // cross-row cascade on the same table can delete an entire
        // subtree implicitly — a category with children must be
        // reassigned/emptied explicitly, not silently cascaded away.
        builder.HasOne<CategoryPersistenceModel>()
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
