using OrderCore.Api.Modules.Messaging.Domain.Entities;

namespace OrderCore.Api.Modules.Messaging.Application.Contracts;

/// <summary>
/// Sends a failed message back to its consumer's own queue — and only
/// there, so other consumers of the same event don't see it twice — as a
/// first attempt, with its original trace context.
/// </summary>
public interface IFailedMessageReplayer
{
    Task ReplayAsync(FailedMessage failedMessage, CancellationToken cancellationToken);
}
