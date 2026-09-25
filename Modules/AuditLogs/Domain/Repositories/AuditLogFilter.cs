namespace OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

/// <summary>
/// Which entries <see cref="IAuditLogRepository.ListPagedAsync"/> returns.
/// Every criterion is optional and they combine with "and": entity name +
/// id is an entity's timeline (e.g. one order), <see cref="UserId"/> is
/// everything one actor did.
/// </summary>
public sealed record AuditLogFilter(string? EntityName, Guid? EntityId, Guid? UserId, string? Action)
{
    public static AuditLogFilter None { get; } = new(null, null, null, null);
}
