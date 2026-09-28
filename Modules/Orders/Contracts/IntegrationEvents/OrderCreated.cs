namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// An order was placed (status <c>Created</c>).
/// Contract <c>orders.order-created</c>, version 1.
/// </summary>
public sealed record OrderCreated : OrderIntegrationEvent
{
    public const string Name = "orders.order-created";
}
