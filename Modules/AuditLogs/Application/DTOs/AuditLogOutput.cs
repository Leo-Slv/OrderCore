using System.Text.Json;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;

namespace OrderCore.Api.Modules.AuditLogs.Application.DTOs;

public sealed class AuditLogOutput
{
    public required Guid Id { get; init; }

    public Guid? UserId { get; init; }

    public required string Action { get; init; }

    public required string EntityName { get; init; }

    public Guid? EntityId { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public required DateTimeOffset CreatedAt { get; init; }

    public static AuditLogOutput FromAuditLog(AuditLog auditLog) => new()
    {
        Id = auditLog.Id,
        UserId = auditLog.UserId,
        Action = auditLog.Action,
        EntityName = auditLog.EntityName,
        EntityId = auditLog.EntityId,
        Metadata = ParseMetadata(auditLog.MetadataJson),
        CreatedAt = auditLog.CreatedAt,
    };

    private static IReadOnlyDictionary<string, string> ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson)
            ?? new Dictionary<string, string>();
    }
}
