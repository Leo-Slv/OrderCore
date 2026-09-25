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

        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.DiscountAmount).HasPrecision(18, 2);
        builder.Property(o => o.ShippingAmount).HasPrecision(18, 2);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 2);

        builder.Property(o => o.ShippingStreet).HasMaxLength(200);
        builder.Property(o => o.ShippingNumber).HasMaxLength(20);
        builder.Property(o => o.ShippingComplement).HasMaxLength(100);
        builder.Property(o => o.ShippingNeighborhood).HasMaxLength(100);
        builder.Property(o => o.ShippingCity).HasMaxLength(100);
        builder.Property(o => o.ShippingState).HasMaxLength(100);
        builder.Property(o => o.ShippingPostalCode).HasMaxLength(20);
        builder.Property(o => o.ShippingCountry).HasMaxLength(100);

        builder.Property(o => o.BillingStreet).HasMaxLength(200);
        builder.Property(o => o.BillingNumber).HasMaxLength(20);
        builder.Property(o => o.BillingComplement).HasMaxLength(100);
        builder.Property(o => o.BillingNeighborhood).HasMaxLength(100);
        builder.Property(o => o.BillingCity).HasMaxLength(100);
        builder.Property(o => o.BillingState).HasMaxLength(100);
        builder.Property(o => o.BillingPostalCode).HasMaxLength(20);
        builder.Property(o => o.BillingCountry).HasMaxLength(100);

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.ConfirmedAt);
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        // Postgres treats NULLs as distinct, so any number of orders without
        // a key (created outside checkout) can coexist under this index.
        builder.Property(o => o.CheckoutIdempotencyKey).HasMaxLength(100);
        builder.HasIndex(o => new { o.CustomerId, o.CheckoutIdempotencyKey }).IsUnique();

        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
