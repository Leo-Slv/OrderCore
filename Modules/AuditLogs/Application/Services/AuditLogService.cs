using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.AuditLogs.Application.Services;

/// <summary>
/// Default <see cref="IAuditLogService"/> implementation. The actor is
/// enriched here, not in callers: when a caller passes no
/// <c>userId</c> (every current call site), the signed-in user from
/// <see cref="ICurrentUser"/> is recorded. With nobody signed in (a
/// background service such as the outbox publisher) the entry has no
/// actor, meaning the system did it. There is no correlation id yet.
/// <para>
/// Recording is best effort. Every caller records after its own change
/// has committed, in a separate transaction, so a failure here can't undo
/// that change; throwing would only turn a request that succeeded into a
/// 500 and invite a retry of a non-idempotent operation. The failure is
/// logged instead. Cancellation still propagates.
/// </para>
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAuditLogRepository auditLogs, TimeProvider timeProvider, ICurrentUser currentUser, ILogger<AuditLogService> logger)
    {
        _auditLogs = auditLogs;
        _timeProvider = timeProvider;
        _currentUser = currentUser;
        _logger = logger;
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
        var auditLog = AuditLog.Create(userId ?? _currentUser.UserId, action, entityName, entityId, metadataJson, _timeProvider.GetUtcNow());

        try
        {
            await _auditLogs.AddAsync(auditLog, cancellationToken);
            await _auditLogs.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception, "Could not record audit entry {Action} for {EntityName} {EntityId}.", action, entityName, entityId);
        }
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
