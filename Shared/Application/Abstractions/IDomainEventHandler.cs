using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Shared.Application.Abstractions;

/// <summary>
/// Reacts to a single domain event type. Registered per module (e.g. the
/// Orders status history projector reacting to Orders domain events — see
/// 05-orders.md) and resolved via DI by
/// <c>InProcessDomainEventDispatcher</c>.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
