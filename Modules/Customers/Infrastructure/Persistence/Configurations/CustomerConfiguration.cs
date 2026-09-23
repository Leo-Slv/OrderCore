using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerPersistenceModel>
{
    public void Configure(EntityTypeBuilder<CustomerPersistenceModel> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.DocumentNumber).HasMaxLength(30);
        builder.Property(c => c.PasswordHash).IsRequired();

        builder.HasIndex(c => c.Email).IsUnique();

        // Optimistic concurrency (section 11 of the project context):
        // AggregateRoot.Version is mapped as a plain concurrency token here
        // rather than a native rowversion column, to stay portable if the
        // module ever needs a database other than PostgreSQL.
        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.HasMany(c => c.Addresses)
            .WithOne(a => a.Customer)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.PaymentMethods)
            .WithOne(m => m.Customer)
            .HasForeignKey(m => m.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
