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
    Task<Guid> RequestPaymentAsync(
        Guid orderId,
        decimal amount,
        string currency,
        PaymentMethodChoice method,
        string idempotencyKey,
        CancellationToken cancellationToken);

    /// <summary>Null when no payment has been requested for the order yet.</summary>
    Task<OrderPaymentSummary?> GetPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken);
}
