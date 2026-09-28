namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The order left for delivery (<c>Shipped</c>).
/// Contract <c>orders.order-shipped</c>, version 1.
/// </summary>
public sealed record OrderShipped : OrderIntegrationEvent
{
    public const string Name = "orders.order-shipped";
}
