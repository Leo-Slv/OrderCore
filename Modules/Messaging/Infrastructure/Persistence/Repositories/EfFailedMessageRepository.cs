using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Repositories;

/// <summary>
/// Same shape as the other EF repositories: keeps the domain instance it
/// handed out next to its tracked persistence model, and reconciles them
/// with <see cref="FailedMessageMapper.ApplyChanges"/> right before saving.
/// </summary>
public sealed class EfFailedMessageRepository : IFailedMessageRepository
{
    private readonly MessagingDbContext _dbContext;
    private readonly Dictionary<Guid, (FailedMessage Domain, FailedMessagePersistenceModel Model)> _tracked = new();

    public EfFailedMessageRepository(MessagingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken)
    {
        var model = FailedMessageMapper.ToPersistence(failedMessage);
        await _dbContext.FailedMessages.AddAsync(model, cancellationToken);
        _tracked[failedMessage.Id] = (failedMessage, model);
    }

    public async Task<FailedMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await _dbContext.FailedMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model is null)
        {
            return null;
        }

        var domain = FailedMessageMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    public async Task<(IReadOnlyList<FailedMessage> Items, int TotalCount)> ListAsync(
        FailedMessageStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.FailedMessages.AsNoTracking();
        if (status is { } wanted)
        {
            var name = wanted.ToString();
            query = query.Where(m => m.Status == name);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(m => m.LastFailedAt)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(FailedMessageMapper.ToDomain).ToList(), totalCount);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            FailedMessageMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
