namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// Stock was reserved and the order is waiting for its payment (<c>PendingPayment</c>).
/// Contract <c>orders.order-payment-requested</c>, version 1.
/// </summary>
public sealed record OrderPaymentRequested : OrderIntegrationEvent
{
    public const string Name = "orders.order-payment-requested";
}
