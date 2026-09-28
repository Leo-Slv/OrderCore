namespace OrderCore.Api.Shared.Application.Messaging;

/// <summary>
/// Reacts to one integration event type delivered by the broker. Registered
/// by the consuming module with its queue
/// (<c>MessagingRegistrationExtensions.AddIntegrationEventConsumer</c>) and
/// invoked by the Messaging module's consumer host in a scope of its own.
/// <para>
/// Delivery is at least once and may be late or out of order (section 21).
/// The host already skips a message this consumer has handled before; the
/// handler must still tolerate state that has moved on (throwing for a
/// message that can never succeed only sends it through every retry to the
/// failed-message list).
/// </para>
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
