using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Shared.Domain;

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
            Version = version,
        };
}
