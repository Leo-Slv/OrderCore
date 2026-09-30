using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// How a module plugs into messaging from its own
/// <c>&lt;Module&gt;DependencyInjection</c> — the module keeps owning its
/// registrations, as for everything else (CLAUDE.md, Dependency Injection).
/// </summary>
public static class MessagingRegistrationExtensions
{
    /// <summary>
    /// Declares an integration event this module publishes, with a stable
    /// contract name (<c>&lt;module&gt;.&lt;event&gt;</c>, e.g.
    /// <c>payments.payment-authorized</c>) and version.
    /// </summary>
    public static IServiceCollection AddIntegrationEvent<TEvent>(this IServiceCollection services, string name, int version)
        where TEvent : IntegrationEvent
    {
        services.IntegrationEventRegistry().AddEvent(typeof(TEvent), name, version);
        return services;
    }

    /// <summary>Tells the relay to drain the outbox mapped into <typeparamref name="TDbContext"/>.</summary>
    public static IServiceCollection AddOutboxSource<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.IntegrationEventRegistry().AddOutboxSource(typeof(TDbContext));
        return services;
    }

    /// <summary>
    /// Subscribes <typeparamref name="THandler"/> to <typeparamref name="TEvent"/>
    /// on <paramref name="queue"/> (<c>&lt;module&gt;.&lt;consumer&gt;</c>), with
    /// its inbox in <typeparamref name="TInboxDbContext"/> — the consuming
    /// module's own context, so the inbox row commits with the handler's
    /// changes. Several handlers can share a queue (one per event type).
    /// </summary>
    public static IServiceCollection AddIntegrationEventConsumer<TEvent, THandler, TInboxDbContext>(
        this IServiceCollection services, string queue)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
        where TInboxDbContext : DbContext
    {
        services.TryAddScoped<THandler>();
        services.IntegrationEventRegistry().AddInboxSource(typeof(TInboxDbContext));
        services.IntegrationEventRegistry().AddConsumer(new ConsumerRegistration(
            queue,
            typeof(TEvent),
            typeof(THandler),
            (provider, integrationEvent, cancellationToken) =>
                provider.GetRequiredService<THandler>().HandleAsync((TEvent)integrationEvent, cancellationToken),
            provider => provider.GetRequiredService<TInboxDbContext>()));
        return services;
    }

    /// <summary>
    /// Declares an inbox kept outside the consumer host (Payments' Stripe
    /// webhooks), so retention cleans it like the consumers' inboxes.
    /// </summary>
    public static IServiceCollection AddInboxSource<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.IntegrationEventRegistry().AddInboxSource(typeof(TDbContext));
        return services;
    }

    /// <summary>The single registry instance, added on first use.</summary>
    public static IntegrationEventRegistry IntegrationEventRegistry(this IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IntegrationEventRegistry));
        if (existing?.ImplementationInstance is IntegrationEventRegistry registry)
        {
            return registry;
        }

        registry = new IntegrationEventRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
