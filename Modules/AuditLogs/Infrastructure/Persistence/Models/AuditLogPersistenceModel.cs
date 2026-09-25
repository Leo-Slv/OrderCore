namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Models;

public sealed class AuditLogPersistenceModel
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
