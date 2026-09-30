using OrderCore.Api.Modules.Notifications.Domain.Entities;

namespace OrderCore.Api.Modules.Notifications.Domain.Repositories;

public interface IEmailMessageRepository
{
    Task AddAsync(EmailMessage message, CancellationToken cancellationToken);

    Task<EmailMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Pending messages due by <paramref name="now"/>, oldest due first.</summary>
    Task<IReadOnlyList<Guid>> ListDueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Removes sent and failed messages that finished before <paramref name="cutoff"/>, at most <paramref name="limit"/>.</summary>
    Task<int> DeleteFinishedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
