namespace OrderCore.Api.Modules.Messaging.Presentation.Responses;

/// <summary>
/// A message a consumer gave up on. <c>type</c> is the event's contract name,
/// <c>consumer</c> the queue it failed on, <c>status</c> <c>Pending</c>,
/// <c>Replayed</c> or <c>Discarded</c>.
/// </summary>
public class FailedMessageSummaryResponse
{
    public Guid Id { get; init; }

    public Guid MessageId { get; init; }

    public string Type { get; init; } = string.Empty;

    public int ContractVersion { get; init; }

    public string Consumer { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int Attempts { get; init; }

    public string LastError { get; init; } = string.Empty;

    public DateTimeOffset FirstFailedAt { get; init; }

    public DateTimeOffset LastFailedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}
