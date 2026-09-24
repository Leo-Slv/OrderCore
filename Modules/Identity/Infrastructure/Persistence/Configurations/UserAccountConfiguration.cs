using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccountPersistenceModel>
{
    public void Configure(EntityTypeBuilder<UserAccountPersistenceModel> builder)
    {
        builder.ToTable("user_accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).HasMaxLength(254).IsRequired();
        builder.Property(a => a.NormalizedEmail).HasMaxLength(254).IsRequired();
        builder.Property(a => a.PasswordHash).IsRequired();
        builder.Property(a => a.Role).HasMaxLength(20).IsRequired();

        // The e-mail is what sign-up reserves and sign-in looks up; one
        // customer has at most one account.
        builder.HasIndex(a => a.NormalizedEmail).IsUnique();
        builder.HasIndex(a => a.CustomerId).IsUnique();

        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.HasMany(a => a.Sessions)
            .WithOne()
            .HasForeignKey(s => s.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
