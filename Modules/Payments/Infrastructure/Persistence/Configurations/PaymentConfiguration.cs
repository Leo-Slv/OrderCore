using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentPersistenceModel>
{
    public void Configure(EntityTypeBuilder<PaymentPersistenceModel> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Method).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Provider).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ProviderReference).HasMaxLength(200);
        builder.Property(p => p.Amount).HasPrecision(18, 2);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.IdempotencyKey).IsUnique();

        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.HasMany(p => p.Refunds)
            .WithOne(r => r.Payment)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
