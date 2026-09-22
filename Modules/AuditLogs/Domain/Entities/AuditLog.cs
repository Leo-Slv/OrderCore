using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.AuditLogs.Domain.Entities;

/// <summary>
/// An immutable record of a state-changing action performed somewhere in
/// the system (login, order created, payment authorized, etc.). AuditLogs
/// is intentionally its own module rather than a cross-cutting concern
/// bolted onto Shared: it has its own persistence, its own read model
/// (paged listing) and, like Payments, is a candidate to eventually be its
/// own bounded context if the audit trail needs to outlive the modules it
/// observes (section 38 — a real variation/boundary justifies this being a
/// module of its own, not a Shared service).
/// </summary>
public sealed class AuditLog : AggregateRoot<Guid>
{
    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityName { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string? MetadataJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid? userId,
        string action,
        string entityName,
        Guid? entityId,
        string? metadataJson,
        DateTimeOffset createdAt) : base(id)
    {
        UserId = userId;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        MetadataJson = metadataJson;
        CreatedAt = createdAt;
    }

    public static AuditLog Create(
        Guid? userId,
        string action,
        string entityName,
        Guid? entityId,
        string? metadataJson,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("Entity name is required.", nameof(entityName));
        }

        var auditLog = new AuditLog(Guid.NewGuid(), userId, action.Trim(), entityName.Trim(), entityId, metadataJson, now);
        auditLog.IncrementVersion();
        return auditLog;
    }
}
