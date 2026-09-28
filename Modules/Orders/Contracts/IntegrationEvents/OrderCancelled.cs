namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// The order was cancelled (<c>Cancelled</c>).
/// Contract <c>orders.order-cancelled</c>, version 1.
/// </summary>
public sealed record OrderCancelled : OrderIntegrationEvent
{
    public const string Name = "orders.order-cancelled";

    public required string Reason { get; init; }
}
