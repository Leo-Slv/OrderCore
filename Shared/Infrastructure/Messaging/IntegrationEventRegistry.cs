using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// What each module told the messaging infrastructure at startup, through
/// <see cref="MessagingRegistrationExtensions"/> in its own
/// <c>&lt;Module&gt;DependencyInjection</c>: the integration events it
/// publishes (contract name + version ↔ CLR type), the <c>DbContext</c>s
/// whose outbox the relay drains, and its consumers (queue, event, handler,
/// and the <c>DbContext</c> holding that consumer's inbox). A single
/// instance, filled while services are registered and read-only afterwards.
/// </summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, Type> _typesByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, EventContract> _contractsByType = new();
    private readonly List<Type> _outboxSources = new();
    private readonly List<Type> _inboxSources = new();
    private readonly List<ConsumerRegistration> _consumers = new();

    public IReadOnlyCollection<Type> OutboxSources => _outboxSources;

    /// <summary>The contexts that hold an inbox (<c>{module}_processed_messages</c>), for retention.</summary>
    public IReadOnlyCollection<Type> InboxSources => _inboxSources;

    public IReadOnlyCollection<ConsumerRegistration> Consumers => _consumers;

    /// <summary>The routing key of a contract: <c>{name}.v{version}</c>.</summary>
    public static string RoutingKey(string name, int version) => $"{name}.v{version}";

    public EventContract ContractOf(Type eventType) =>
        _contractsByType.TryGetValue(eventType, out var contract)
            ? contract
            : throw new InvalidOperationException(
                $"Integration event '{eventType.Name}' isn't registered; call AddIntegrationEvent in its module.");

    /// <summary>Null for a type no module registered (e.g. a newer version this build doesn't know).</summary>
    public Type? TypeOf(string name, int version) =>
        _typesByKey.GetValueOrDefault(RoutingKey(name, version));

    internal void AddEvent(Type eventType, string name, int version)
    {
        var key = RoutingKey(name, version);
        if (_typesByKey.TryGetValue(key, out var existing) && existing != eventType)
        {
            throw new InvalidOperationException($"Integration event '{key}' is already registered for '{existing.Name}'.");
        }

        _typesByKey[key] = eventType;
        _contractsByType[eventType] = new EventContract(name, version);
    }

    internal void AddOutboxSource(Type dbContextType)
    {
        if (!_outboxSources.Contains(dbContextType))
        {
            _outboxSources.Add(dbContextType);
        }
    }

    internal void AddInboxSource(Type dbContextType)
    {
        if (!_inboxSources.Contains(dbContextType))
        {
            _inboxSources.Add(dbContextType);
        }
    }

    internal void AddConsumer(ConsumerRegistration consumer) => _consumers.Add(consumer);
}

/// <summary>How an integration event travels: its contract name and version.</summary>
public sealed record EventContract(string Name, int Version)
{
    public string RoutingKey => IntegrationEventRegistry.RoutingKey(Name, Version);
}

/// <summary>
/// One handler subscribed to one event type on one queue. <see cref="Invoke"/>
/// and <see cref="InboxOf"/> are built from the generic registration, so the
/// consumer host needs no reflection to call the handler or reach the inbox.
/// </summary>
public sealed record ConsumerRegistration(
    string Queue,
    Type EventType,
    Type HandlerType,
    Func<IServiceProvider, IntegrationEvent, CancellationToken, Task> Invoke,
    Func<IServiceProvider, DbContext> InboxOf);
