using OrderCore.Api.Shared.Application.Messaging;

namespace OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;

/// <summary>What every event about an order's reserved stock says.</summary>
public abstract record StockReservationIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid OrderId { get; init; }

    public required Guid ProductId { get; init; }

    public required int Quantity { get; init; }
}
