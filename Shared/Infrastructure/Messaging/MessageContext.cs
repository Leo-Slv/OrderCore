using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// Scoped <see cref="IMessageContext"/>: empty in an HTTP request, set by the
/// consumer host at the start of each message's scope.
/// </summary>
public sealed class MessageContext : IMessageContext
{
    public Guid? CausationId { get; private set; }

    public void BeginHandling(Guid messageId) => CausationId = messageId;
}
