using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Mappers;

/// <summary>
/// No <c>ApplyChanges</c>: an entry is never changed after it is written.
/// </summary>
public static class AuditLogMapper
{
    public static AuditLog ToDomain(AuditLogPersistenceModel model) =>
        AuditLog.Rehydrate(
            model.Id, model.UserId, model.Action, model.EntityName, model.EntityId, model.MetadataJson, model.CreatedAt);

    public static AuditLogPersistenceModel ToPersistence(AuditLog auditLog) => new()
    {
        Id = auditLog.Id,
        UserId = auditLog.UserId,
        Action = auditLog.Action,
        EntityName = auditLog.EntityName,
        EntityId = auditLog.EntityId,
        MetadataJson = auditLog.MetadataJson,
        CreatedAt = auditLog.CreatedAt,
    };
}
