namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The order was delivered (<c>Delivered</c>).
/// Contract <c>orders.order-delivered</c>, version 1.
/// </summary>
public sealed record OrderDelivered : OrderIntegrationEvent
{
    public const string Name = "orders.order-delivered";
}
