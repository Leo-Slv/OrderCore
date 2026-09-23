using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItemPersistenceModel>
{
    public void Configure(EntityTypeBuilder<StockItemPersistenceModel> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(s => s.Id);

        // Unique on ProductId alone, not (ProductId, ProductVariantId):
        // GetByProductIdAsync (Application contract) only takes a
        // productId, with no way to disambiguate by variant, so today
        // exactly one StockItem per product is what makes that lookup
        // well-defined. ProductVariantId stays as a column for a future
        // per-variant stock feature, same as StockItem.ReorderLevel having
        // no setter yet.
        builder.HasIndex(s => s.ProductId).IsUnique();

        builder.Property(s => s.Version).IsConcurrencyToken();
    }
}
