using OrderCore.Api.Modules.AuditLogs.Domain.Entities;

namespace OrderCore.Api.Modules.AuditLogs.Domain.Repositories;

/// <summary>
/// Persistence abstraction the Application layer depends on (section 38 —
/// justified the same way <c>IOrderRepository</c> is: a real implementation
/// swap, from the in-memory one used today to EF Core later, and
/// testability, both exist here).
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AuditLog>> ListByEntityAsync(
        string entityName,
        Guid entityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AuditLog>> ListByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
