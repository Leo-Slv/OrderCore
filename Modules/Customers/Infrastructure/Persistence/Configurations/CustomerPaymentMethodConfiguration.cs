using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class CustomerPaymentMethodConfiguration : IEntityTypeConfiguration<CustomerPaymentMethodPersistenceModel>
{
    public void Configure(EntityTypeBuilder<CustomerPaymentMethodPersistenceModel> builder)
    {
        builder.ToTable("customer_payment_methods");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Provider).HasMaxLength(50).IsRequired();
        builder.Property(m => m.ProviderCustomerReference).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Brand).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Last4Digits).HasMaxLength(4).IsRequired();

        builder.HasIndex(m => m.CustomerId);
    }
}
