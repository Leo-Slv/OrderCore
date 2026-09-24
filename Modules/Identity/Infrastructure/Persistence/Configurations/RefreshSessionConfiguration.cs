using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSessionPersistenceModel>
{
    public void Configure(EntityTypeBuilder<RefreshSessionPersistenceModel> builder)
    {
        builder.ToTable("refresh_sessions");

        builder.HasKey(s => s.Id);

        // Assigned by the domain; see the child-key rule in CLAUDE.md.
        builder.Property(s => s.Id).ValueGeneratedNever();

        // SHA-256, hex-encoded.
        builder.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();
    }
}
