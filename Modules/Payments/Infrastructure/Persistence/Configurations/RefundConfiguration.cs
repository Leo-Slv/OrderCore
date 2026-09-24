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

        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Amount).HasPrecision(18, 2);

        builder.HasIndex(r => r.PaymentId);
    }
}
