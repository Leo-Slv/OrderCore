namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>The order's payment in full, for the backoffice.</summary>
public sealed class OrderPaymentDetailsResponse
{
    public Guid PaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string? ProviderReference { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? AuthorizedAt { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }

    public DateTimeOffset? VoidedAt { get; init; }

    public IReadOnlyList<OrderRefundResponse> Refunds { get; init; } = [];
}
