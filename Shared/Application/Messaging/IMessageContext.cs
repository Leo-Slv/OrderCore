namespace OrderCore.Api.Shared.Application.Messaging;

/// <summary>
/// The message being handled in the current scope, if any. Set by the
/// Messaging module's consumer host before it invokes a handler, and read
/// by the outbox so an event a handler publishes names the message that
/// caused it. Outside a consumer (an HTTP request) there is none.
/// </summary>
public interface IMessageContext
{
    Guid? CausationId { get; }
}
