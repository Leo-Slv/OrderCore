using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.AuditLogs.Domain.Entities;
using OrderCore.Api.Modules.AuditLogs.Domain.Repositories;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Mappers;

namespace OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence.Repositories;

/// <summary>
/// Simpler than the other modules' repositories: entries are append-only,
/// so there is nothing handed out that could change and need reconciling
/// before a save.
/// </summary>
public sealed class EfAuditLogRepository : IAuditLogRepository
{
    private readonly AuditLogsDbContext _dbContext;

    public EfAuditLogRepository(AuditLogsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken) =>
        await _dbContext.AuditLogs.AddAsync(AuditLogMapper.ToPersistence(auditLog), cancellationToken);

    /// <summary>
    /// On failure the pending entries are dropped, so that one failed write
    /// isn't retried (and failed again) by every later record in the same
    /// request scope.
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<(IReadOnlyCollection<AuditLog> Items, int TotalCount)> ListPagedAsync(
        AuditLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (filter.EntityName is { } entityName)
        {
            query = query.Where(a => a.EntityName == entityName);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (filter.UserId is { } userId)
        {
            query = query.Where(a => a.UserId == userId);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(AuditLogMapper.ToDomain).ToList(), totalCount);
    }
}
