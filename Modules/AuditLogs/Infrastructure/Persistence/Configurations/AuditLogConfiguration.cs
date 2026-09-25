using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only table: entries are never updated, so there is no
/// concurrency token. The indexes follow the listing's filters, each
/// ending in <c>CreatedAt</c> because every listing is newest first.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogPersistenceModel>
{
    public void Configure(EntityTypeBuilder<AuditLogPersistenceModel> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();

        builder.HasIndex(a => new { a.EntityName, a.EntityId, a.CreatedAt });
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });
        builder.HasIndex(a => a.CreatedAt);
    }
}
