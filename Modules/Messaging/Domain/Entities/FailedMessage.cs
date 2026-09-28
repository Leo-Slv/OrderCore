using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Messaging.Domain.Entities;

/// <summary>
/// A message one consumer couldn't handle after every attempt (or couldn't
/// read at all), set aside so it no longer blocks the queue behind it
/// (Docs/specs/events/async-messaging.md, decision 5). Keeps everything
/// needed to send it back to the same consumer as it was: the envelope,
/// its trace context and why it failed.
/// </summary>
public sealed class FailedMessage : AggregateRoot<Guid>
{
    public Guid MessageId { get; private set; }

    /// <summary>The contract name, e.g. <c>payments.payment-authorized</c>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>The contract version (the aggregate's own <c>Version</c> is its concurrency token).</summary>
    public int ContractVersion { get; private set; }

    /// <summary>The consumer's queue — replaying sends the message back there only.</summary>
    public string Consumer { get; private set; } = string.Empty;

    /// <summary>The whole envelope, as received.</summary>
    public string Body { get; private set; } = string.Empty;

    public string? TraceParent { get; private set; }

    public string? TraceState { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public int Attempts { get; private set; }

    public DateTimeOffset FirstFailedAt { get; private set; }

    public DateTimeOffset LastFailedAt { get; private set; }

    public FailedMessageStatus Status { get; private set; }

    /// <summary>When an admin replayed or discarded it; <c>null</c> while <see cref="FailedMessageStatus.Pending"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    private FailedMessage()
    {
    }

    private FailedMessage(Guid id) : base(id)
    {
    }

    public const int MaxErrorLength = 4000;

    public static FailedMessage Record(
        Guid messageId,
        string type,
        int contractVersion,
        string consumer,
        string body,
        string? traceParent,
        string? traceState,
        string lastError,
        int attempts,
        DateTimeOffset firstFailedAt,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(consumer))
        {
            throw new ArgumentException("The consumer is required.", nameof(consumer));
        }

        var failed = new FailedMessage(Guid.NewGuid())
        {
            MessageId = messageId,
            Type = string.IsNullOrWhiteSpace(type) ? "unknown" : type,
            ContractVersion = contractVersion,
            Consumer = consumer,
            Body = body,
            TraceParent = traceParent,
            TraceState = traceState,
            LastError = lastError.Length > MaxErrorLength ? lastError[..MaxErrorLength] : lastError,
            Attempts = attempts,
            FirstFailedAt = firstFailedAt,
            LastFailedAt = now,
            Status = FailedMessageStatus.Pending,
        };
        failed.IncrementVersion();
        return failed;
    }

    /// <summary>
    /// Checked before sending the message back, so a message already
    /// replayed or discarded is never sent again.
    /// </summary>
    public void EnsurePending()
    {
        if (Status != FailedMessageStatus.Pending)
        {
            throw new DomainRuleViolationException(
                "invalid_failed_message_state", $"Failed message '{Id}' was already {Status.ToString().ToLowerInvariant()}.");
        }
    }

    /// <summary>The message was sent back to its consumer's queue, with a fresh count of attempts.</summary>
    public void MarkReplayed(DateTimeOffset now)
    {
        EnsurePending();
        Status = FailedMessageStatus.Replayed;
        ResolvedAt = now;
        IncrementVersion();
    }

    /// <summary>An admin decided the message will never be handled; it stays here for the record.</summary>
    public void Discard(DateTimeOffset now)
    {
        EnsurePending();
        Status = FailedMessageStatus.Discarded;
        ResolvedAt = now;
        IncrementVersion();
    }

    internal static FailedMessage Rehydrate(
        Guid id,
        Guid messageId,
        string type,
        int contractVersion,
        string consumer,
        string body,
        string? traceParent,
        string? traceState,
        string lastError,
        int attempts,
        DateTimeOffset firstFailedAt,
        DateTimeOffset lastFailedAt,
        FailedMessageStatus status,
        DateTimeOffset? resolvedAt,
        int version) =>
        new(id)
        {
            MessageId = messageId,
            Type = type,
            ContractVersion = contractVersion,
            Consumer = consumer,
            Body = body,
            TraceParent = traceParent,
            TraceState = traceState,
            LastError = lastError,
            Attempts = attempts,
            FirstFailedAt = firstFailedAt,
            LastFailedAt = lastFailedAt,
            Status = status,
            ResolvedAt = resolvedAt,
            Version = version,
        };
}
