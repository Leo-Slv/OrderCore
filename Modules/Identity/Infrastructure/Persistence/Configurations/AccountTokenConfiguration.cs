using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountTokenPersistenceModel>
{
    public void Configure(EntityTypeBuilder<AccountTokenPersistenceModel> builder)
    {
        builder.ToTable("account_tokens");

        builder.HasKey(t => t.Id);

        // Assigned by the domain; see the child-key rule in CLAUDE.md.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Purpose).HasMaxLength(30).IsRequired();

        // SHA-256, hex-encoded; a reset or confirmation link is looked up by it.
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
    }
}
