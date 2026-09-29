namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <see cref="Status"/> is the payment's own status (<c>Pending</c>,
/// <c>Processing</c>, <c>Authorized</c>, <c>Captured</c>, <c>Failed</c>,
/// <c>Refunded</c>). <see cref="FailureReason"/> is the provider's reason
/// code (e.g. <c>card_declined</c>) when it failed.
/// </summary>
public sealed class OrderPaymentResponse
{
    public Guid PaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? FailureReason { get; init; }

    /// <summary>
    /// What the buyer must do for the payment to go ahead — only in the
    /// checkout response (a replay with the same <c>Idempotency-Key</c>
    /// returns it again), null elsewhere and with the fake provider.
    /// </summary>
    public OrderPaymentNextActionResponse? NextAction { get; init; }
}
