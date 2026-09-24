using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Outbox;

/// <summary>
/// Maps an <see cref="IntegrationEvent"/>'s CLR type to/from the plain
/// string stored in <see cref="OutboxMessage.Type"/> — needed by both
/// <see cref="OutboxWriter"/> (writing) and
/// <see cref="OutboxPublisherBackgroundService"/> (reading back for
/// deserialization). Not in 06-payments.md: a real serialization concern
/// the diagram's `Enqueue(IntegrationEvent)`/`Type` string leave implicit.
/// </summary>
internal static class IntegrationEventTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> ByName = new Dictionary<string, Type>
    {
        [nameof(PaymentRequested)] = typeof(PaymentRequested),
        [nameof(PaymentAuthorized)] = typeof(PaymentAuthorized),
        [nameof(PaymentFailed)] = typeof(PaymentFailed),
        [nameof(PaymentRefunded)] = typeof(PaymentRefunded),
    };

    public static string NameOf(IntegrationEvent integrationEvent) => integrationEvent.GetType().Name;

    public static Type Resolve(string typeName) =>
        ByName.TryGetValue(typeName, out var type)
            ? type
            : throw new InvalidOperationException($"Unknown integration event type '{typeName}'.");
}
