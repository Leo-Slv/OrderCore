namespace OrderCore.Api.Modules.AuditLogs.Application.Services;

/// <summary>
/// Contract other modules depend on to record an audit entry (section 30 —
/// observability/auditoria). Deliberately not <c>IAuditLogRepository</c>
/// directly: this is the Application-level contract a use case in Orders,
/// Payments, etc. would call, keeping the JSON metadata encoding and
/// timestamping an implementation detail of AuditLogs (section 7 — the
/// same "Application Contract" indirection used for cross-module
/// dependencies).
/// </summary>
public interface IAuditLogService
{
    Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId,
        IReadOnlyDictionary<string, string?>? metadata,
        Guid? userId,
        CancellationToken cancellationToken);
}
