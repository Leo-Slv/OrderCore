using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// Read-side/command contract Orders uses to reach the Payments module —
/// the "Application Contract" indirection from section 7, implemented by
/// <c>PaymentGatewayAdapter</c> (Infrastructure/Adapters), which wraps
/// Payments' <c>CreatePaymentUseCase</c>. See 05-orders.md.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>The methods checkout may accept now (the configured provider's).</summary>
    IReadOnlyCollection<PaymentMethodChoice> GetAvailableMethods();

    /// <returns>What the buyer must do next (confirm the card with Stripe); null when the provider answered at once.</returns>
    Task<OrderPaymentNextAction?> RequestPaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        PaymentMethodChoice method,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// The next action of a payment already requested and still waiting for
    /// the buyer (a replayed checkout); null otherwise.
    /// </summary>
    Task<OrderPaymentNextAction?> GetPaymentNextActionAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Null when no payment has been requested for the order yet.</summary>
    Task<OrderPaymentSummary?> GetPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Payments of several orders in one call; orders without one are absent.</summary>
    Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetPaymentSummariesAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

    /// <summary>How many authorized payments expire before <paramref name="cutoff"/> (for the dashboard).</summary>
    Task<int> CountAuthorizationsExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Null when no payment has been requested for the order.</summary>
    Task<OrderPaymentDetails?> GetPaymentDetailsAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Captures the order's authorized payment when it ships (backoffice
    /// decision 1). Idempotent. A provider refusal comes through as
    /// Payments' <c>409 payment_capture_failed</c>.
    /// </summary>
    Task CaptureForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Leaves no money held for an order being cancelled (backoffice
    /// decision 2): voids an authorization, refunds a capture. Idempotent.
    /// Payments' <c>409 payment_in_progress</c>/<c>payment_void_failed</c>/
    /// <c>payment_refund_failed</c> come through unchanged.
    /// </summary>
    Task<OrderPaymentSettlement> SettleForCancellationAsync(Guid orderId, string reason, CancellationToken cancellationToken);
}
