namespace OrderCore.Api.Modules.Messaging.Presentation.Responses;

/// <summary>
/// A failed message in full: <c>body</c> is the envelope exactly as the
/// consumer received it, and <c>traceParent</c> the trace that led to it.
/// </summary>
public sealed class FailedMessageDetailsResponse : FailedMessageSummaryResponse
{
    public string? TraceParent { get; init; }

    public string Body { get; init; } = string.Empty;
}
