using System.Text.Json;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

namespace OrderCore.Api.Modules.AuditLogs.Application.Services;

/// <summary>
/// Default <see cref="IAuditLogService"/> implementation. Unlike
/// CourseCore's, this does not enrich entries with a current-user or
/// correlation id sourced from an <c>ICurrentUserService</c> /
/// <c>IRequestContextService</c> — OrderCore has no auth/request-context
/// infrastructure yet (section 32), so callers pass <paramref
/// name="userId"/> explicitly for now. Add that enrichment here, not in
/// callers, once those services exist.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly TimeProvider _timeProvider;

    public AuditLogService(IAuditLogRepository auditLogs, TimeProvider timeProvider)
    {
        _auditLogs = auditLogs;
        _timeProvider = timeProvider;
    }

    public async Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId,
        IReadOnlyDictionary<string, string?>? metadata,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var metadataJson = BuildMetadataJson(metadata);
        var auditLog = AuditLog.Create(userId, action, entityName, entityId, metadataJson, _timeProvider.GetUtcNow());

        await _auditLogs.AddAsync(auditLog, cancellationToken);
        await _auditLogs.SaveChangesAsync(cancellationToken);
    }

    private static string? BuildMetadataJson(IReadOnlyDictionary<string, string?>? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var sanitized = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            sanitized[key.Trim()] = value.Trim();
        }

        return sanitized.Count == 0 ? null : JsonSerializer.Serialize(sanitized);
    }
}
