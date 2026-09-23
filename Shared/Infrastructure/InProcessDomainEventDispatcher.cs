using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Shared.Infrastructure;

/// <summary>
/// Resolves every <see cref="IDomainEventHandler{TEvent}"/> registered for
/// each event's runtime type via DI and invokes them in-process, within the
/// same unit of work the aggregate was saved in — no message broker or
/// outbox involved (that's what Integration Events / Transactional Outbox
/// are for, see IDomainEvent's remarks). See 01-shared-kernel.md.
/// </summary>
public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public InProcessDomainEventDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());

            foreach (var handler in _serviceProvider.GetServices(handlerType))
            {
                if (handler is null)
                {
                    continue;
                }

                var handleAsync = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                await ((Task)handleAsync.Invoke(handler, [domainEvent, cancellationToken])!).ConfigureAwait(false);
            }
        }
    }
}
