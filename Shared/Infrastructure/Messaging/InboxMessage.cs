namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// "Message <see cref="MessageId"/> was handled by <see cref="Consumer"/>",
/// in the consuming module's own table (<c>&lt;module&gt;_processed_messages</c>,
/// mapped by <see cref="MessagingModelBuilderExtensions.AddInbox"/>). Saved
/// in the same transaction as the handler's changes; its key makes a second
/// delivery of the same message to the same consumer a no-op, even when two
/// copies arrive at once.
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    /// <summary>The consumer's queue name.</summary>
    public string Consumer { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
