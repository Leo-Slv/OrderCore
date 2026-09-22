using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Repositories;

/// <summary>
/// In-memory stand-in for the eventual EF Core repository, the same role
/// <c>FakePaymentProvider</c> plays for Payments (section 14): no
/// persistence layer exists anywhere in OrderCore yet (section 40), so
/// this lets the module be registered, called and tested end-to-end today.
/// Replace with an EF-backed repository (Configurations/Mappers/Models
/// under this same folder) once a DbContext exists — do not extend this
/// type with query methods it doesn't need in the meantime (section 38).
/// </summary>
public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly List<AuditLog> _auditLogs = new();
    private readonly object _lock = new();

    public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _auditLogs.Add(auditLog);
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyCollection<AuditLog>> ListByEntityAsync(
        string entityName,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyCollection<AuditLog> result = _auditLogs
                .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyCollection<AuditLog>> ListByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyCollection<AuditLog> result = _auditLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var totalCount = _auditLogs.Count;
            IReadOnlyCollection<AuditLog> items = _auditLogs
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult((items, totalCount));
        }
    }
}
