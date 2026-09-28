namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The order is being prepared (<c>Processing</c>).
/// Contract <c>orders.order-processing-started</c>, version 1.
/// </summary>
public sealed record OrderProcessingStarted : OrderIntegrationEvent
{
    public const string Name = "orders.order-processing-started";
}
