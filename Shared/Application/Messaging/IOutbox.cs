namespace OrderCore.Api.Shared.Application.Messaging;

/// <summary>
/// Records an integration event in the publishing module's own outbox, as
/// part of the save that changes the aggregate — so "the change happened"
/// and "the event will be published" commit together or not at all
/// (section 20). Nothing is sent to the broker here: the Messaging module's
/// relay publishes committed rows. Never publish to the broker directly.
/// </summary>
public interface IOutbox
{
    /// <summary>Adds the event to the current unit of work; it is written by the next <c>SaveChangesAsync</c>.</summary>
    void Enqueue(IntegrationEvent integrationEvent);
}
