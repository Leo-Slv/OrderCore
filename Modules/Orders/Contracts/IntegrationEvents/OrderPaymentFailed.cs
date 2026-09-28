namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The payment was refused and the order's stock released (<c>PaymentFailed</c>).
/// Contract <c>orders.order-payment-failed</c>, version 1.
/// </summary>
public sealed record OrderPaymentFailed : OrderIntegrationEvent
{
    public const string Name = "orders.order-payment-failed";

    public required string Reason { get; init; }
}
