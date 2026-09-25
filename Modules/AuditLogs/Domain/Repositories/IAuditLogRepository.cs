using OrderCore.Api.Modules.AuditLogs.Domain.Entities;

namespace OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

/// <summary>
/// Persistence abstraction the Application layer depends on, implemented
/// by <c>EfAuditLogRepository</c>. Entries are append-only: there is no
/// update or delete.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One page of the entries matching <paramref name="filter"/>, newest
    /// first, plus how many match in total.
    /// </summary>
    Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        AuditLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
