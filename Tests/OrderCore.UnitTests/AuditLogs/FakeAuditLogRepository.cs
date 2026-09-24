using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

namespace OrderCore.UnitTests.AuditLogs;

public sealed class FakeAuditLogRepository : IAuditLogRepository
{
    private readonly List<AuditLog> _auditLogs = new();

    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        _auditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyCollection<AuditLog>> ListByEntityAsync(string entityName, Guid entityId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<AuditLog>>(
            _auditLogs.Where(a => a.EntityName == entityName && a.EntityId == entityId).ToList());

    public Task<IReadOnlyCollection<AuditLog>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<AuditLog>>(_auditLogs.Where(a => a.UserId == userId).ToList());

    public Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = _auditLogs.OrderByDescending(a => a.CreatedAt).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyCollection<AuditLog>, int)>((items, ordered.Count));
    }
}
