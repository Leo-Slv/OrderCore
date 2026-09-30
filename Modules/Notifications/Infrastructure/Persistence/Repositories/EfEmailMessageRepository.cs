using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// Same shape as the other EF repositories: keeps the domain instance it
/// handed out next to its tracked persistence model, and reconciles them
/// with <see cref="EmailMessageMapper.ApplyChanges"/> right before saving.
/// </summary>
public sealed class EfEmailMessageRepository : IEmailMessageRepository
{
    private static readonly string Pending = EmailStatus.Pending.ToString();

    private readonly NotificationsDbContext _dbContext;
    private readonly Dictionary<Guid, (EmailMessage Domain, EmailMessagePersistenceModel Model)> _tracked = new();

    public EfEmailMessageRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var model = EmailMessageMapper.ToPersistence(message);
        await _dbContext.Emails.AddAsync(model, cancellationToken);
        _tracked[message.Id] = (message, model);
    }

    public async Task<EmailMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await _dbContext.Emails.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (model is null)
        {
            return null;
        }

        var domain = EmailMessageMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    public async Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        await _dbContext.Emails
            .Where(m => m.Status == Pending && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Select(m => m.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<int> DeleteFinishedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken) =>
        _dbContext.Emails
            .Where(m => (m.SentAt != null && m.SentAt < cutoff) || (m.FailedAt != null && m.FailedAt < cutoff))
            .Take(limit)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            EmailMessageMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
