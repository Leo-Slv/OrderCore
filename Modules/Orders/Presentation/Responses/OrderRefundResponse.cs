namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

public sealed class OrderRefundResponse
{
    public Guid Id { get; init; }

    public decimal Amount { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary><c>Pending</c>, <c>Completed</c> or <c>Failed</c>.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; init; }
}
