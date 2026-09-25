namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

/// <summary>
/// <see cref="Status"/> is <c>Pending</c>, <c>Completed</c> or <c>Failed</c>;
/// <see cref="ProcessedAt"/> is when the provider answered.
/// </summary>
public sealed class RefundResponse
{
    public Guid Id { get; init; }

    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }
}
