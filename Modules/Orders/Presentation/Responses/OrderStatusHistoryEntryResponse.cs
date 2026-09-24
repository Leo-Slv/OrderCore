namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <see cref="FromStatus"/> is null when the previous status can't be
/// told from the transition alone (the first entry, or a cancellation).
/// </summary>
public sealed class OrderStatusHistoryEntryResponse
{
    public string? FromStatus { get; init; }

    public string ToStatus { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}
