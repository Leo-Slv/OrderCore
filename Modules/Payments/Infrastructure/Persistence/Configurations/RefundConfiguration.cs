using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Configurations;

public sealed class RefundConfiguration : IEntityTypeConfiguration<RefundPersistenceModel>
{
    public void Configure(EntityTypeBuilder<RefundPersistenceModel> builder)
    {
        builder.ToTable("refunds");

        builder.HasKey(r => r.Id);

        // The id is assigned by the domain, not the database. Without this, EF
        // Core treats a new child that already has a key, found while saving
        // its parent, as an existing row and runs an UPDATE (0 rows, concurrency
        // exception) instead of an INSERT.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Amount).HasPrecision(18, 2);

        builder.HasIndex(r => r.PaymentId);
    }
}
