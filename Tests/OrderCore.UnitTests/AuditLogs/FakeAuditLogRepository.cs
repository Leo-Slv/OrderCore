using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class FakeAuditLogRepository : IAuditLogRepository
{
    private readonly List<AuditLog> _auditLogs = new();

    /// <summary>When set, <see cref="SaveChangesAsync"/> throws it.</summary>
    public Exception? SaveFailure { get; set; }

    public IReadOnlyList<AuditLog> Stored => _auditLogs;

    public AuditLogFilter? LastFilter { get; private set; }

    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        _auditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveFailure is null ? Task.CompletedTask : Task.FromException(SaveFailure);

    public Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastFilter = filter;
        var matching = _auditLogs
            .Where(a => filter.EntityName is null || a.EntityName == filter.EntityName)
            .Where(a => filter.EntityId is null || a.EntityId == filter.EntityId)
            .Where(a => filter.UserId is null || a.UserId == filter.UserId)
            .Where(a => filter.Action is null || a.Action == filter.Action)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyCollection<AuditLog>, int)>((items, matching.Count));
    }
}
