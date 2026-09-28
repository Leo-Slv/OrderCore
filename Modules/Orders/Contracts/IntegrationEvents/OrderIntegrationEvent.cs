using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

/// <summary>
/// What every Orders event says about the order: enough to show the change
/// (the Tracking feature pushes it to the browser) without reading the
/// order again. <see cref="Status"/> is the status the order moved to.
/// </summary>
public abstract record OrderIntegrationEvent : IntegrationEvent
{
    public required Guid OrderId { get; init; }

    public required string OrderNumber { get; init; }

    public required Guid CustomerId { get; init; }

    public required string Status { get; init; }

    public required decimal TotalAmount { get; init; }

    public required string Currency { get; init; }
}
