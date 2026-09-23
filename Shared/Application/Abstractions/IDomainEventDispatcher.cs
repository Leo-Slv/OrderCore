using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Shared.Application.Abstractions;

/// <summary>
/// Dispatches domain events raised by an aggregate to whichever
/// <see cref="IDomainEventHandler{TEvent}"/> is registered for each event's
/// type. Implemented by <c>InProcessDomainEventDispatcher</c> and invoked by
/// repositories after a successful save, within the same unit of work — see
/// 01-shared-kernel.md.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken);
}
