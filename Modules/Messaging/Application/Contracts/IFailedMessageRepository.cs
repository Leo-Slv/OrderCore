using OrderCore.Api.Modules.Messaging.Domain.Entities;

namespace OrderCore.Api.Modules.Messaging.Application.Contracts;

/// <summary>Where the consumer host sets aside messages that exhausted their attempts.</summary>
public interface IFailedMessageRepository
{
    Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
