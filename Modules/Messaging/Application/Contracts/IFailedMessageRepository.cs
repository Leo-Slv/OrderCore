using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Domain.Enums;

namespace OrderCore.Api.Modules.Messaging.Application.Contracts;

/// <summary>
/// Where the consumer host sets aside messages that exhausted their
/// attempts, and where the backoffice finds them. Same shape as the other
/// repositories: changes to an aggregate handed out here are saved by
/// <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IFailedMessageRepository
{
    Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken);

    Task<FailedMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Most recent failure first.</summary>
    Task<(IReadOnlyList<FailedMessage> Items, int TotalCount)> ListAsync(
        FailedMessageStatus? status, int page, int pageSize, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
