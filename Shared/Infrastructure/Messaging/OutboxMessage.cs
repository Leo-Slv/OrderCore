namespace OrderCore.Api.Shared.Infrastructure.Messaging;

/// <summary>
/// One integration event waiting to be published, in the publishing
/// module's own table (<c>&lt;module&gt;_outbox_messages</c>, mapped by
/// <see cref="MessagingModelBuilderExtensions.AddOutbox"/>). Written in the
/// same save as the aggregate that raised it; read and marked sent by the
/// Messaging module's relay once the broker has confirmed it.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>The event id, which is also the message id consumers deduplicate on.</summary>
    public Guid Id { get; set; }

    /// <summary>The registered contract name, e.g. <c>payments.payment-authorized</c>.</summary>
    public string Type { get; set; } = string.Empty;

    public int Version { get; set; }

    /// <summary>The event itself, as JSON.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>W3C trace context of the request or consumer that wrote the event.</summary>
    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }

    /// <summary>The message whose handling caused this event, if any.</summary>
    public Guid? CausationId { get; set; }

    /// <summary>When the broker confirmed it; null while waiting.</summary>
    public DateTimeOffset? SentAt { get; set; }

    public int PublishAttempts { get; set; }

    public string? LastError { get; set; }
}
