namespace OrderCore.Api.Modules.Payments.Presentation.Responses;

/// <summary>A row of the backoffice payment list; the full payment is <c>GET payments/{id}</c>.</summary>
public sealed class PaymentSummaryResponse
{
    public Guid Id { get; init; }

    public Guid OrderId { get; init; }

    public decimal Amount { get; init; }

    /// <summary>Sum of the completed refunds.</summary>
    public decimal RefundedAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
