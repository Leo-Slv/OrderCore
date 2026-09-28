namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The payment was authorized and the order is confirmed (<c>Confirmed</c>).
/// Contract <c>orders.order-confirmed</c>, version 1.
/// </summary>
public sealed record OrderConfirmed : OrderIntegrationEvent
{
    public const string Name = "orders.order-confirmed";
}
