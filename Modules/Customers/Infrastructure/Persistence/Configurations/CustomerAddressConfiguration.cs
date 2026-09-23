using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddressPersistenceModel>
{
    public void Configure(EntityTypeBuilder<CustomerAddressPersistenceModel> builder)
    {
        builder.ToTable("customer_addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Label).HasMaxLength(100).IsRequired();
        builder.Property(a => a.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Phone).HasMaxLength(30);
        builder.Property(a => a.Street).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Number).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.Neighborhood).HasMaxLength(100).IsRequired();
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.State).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PostalCode).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Country).HasMaxLength(100).IsRequired();

        builder.HasIndex(a => a.CustomerId);
    }
}
