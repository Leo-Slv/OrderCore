using OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Application.Messaging;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Messaging;

/// <summary>
/// Turns Inventory's domain events into its contracts: the movements of an
/// order's reservation (reserved, released, consumed, returned) and stock
/// alerts. Receipts and adjustments stay internal (the movement history).
/// The integration event keeps the domain event's id.
/// </summary>
public static class InventoryIntegrationEventTranslator
{
    public static IntegrationEvent? Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        InventoryStockMovementRecorded { MovementType: StockMovementType.ReservationCreated, OrderId: { } orderId } e => new StockReserved
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            ReservationId = e.ReferenceId,
            OrderId = orderId,
            ProductId = e.ProductId,
            Quantity = e.Quantity,
        },
        InventoryStockMovementRecorded { MovementType: StockMovementType.ReservationReleased, OrderId: { } orderId } e => new StockReleased
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            ReservationId = e.ReferenceId,
            OrderId = orderId,
            ProductId = e.ProductId,
            Quantity = e.Quantity,
        },
        InventoryStockMovementRecorded { MovementType: StockMovementType.ReservationConsumed, OrderId: { } orderId } e => new StockConsumed
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            ReservationId = e.ReferenceId,
            OrderId = orderId,
            ProductId = e.ProductId,
            Quantity = e.Quantity,
        },
        InventoryStockMovementRecorded { MovementType: StockMovementType.ReservationReturned, OrderId: { } orderId } e => new StockReturned
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            ReservationId = e.ReferenceId,
            OrderId = orderId,
            ProductId = e.ProductId,
            Quantity = e.Quantity,
        },
        StockAlertRaised e => new StockAlert
        {
            EventId = e.EventId,
            Version = 1,
            OccurredAt = e.OccurredAt,
            ProductId = e.ProductId,
            Level = e.Level.ToString(),
            QuantityAvailable = e.QuantityAvailable,
            ReorderLevel = e.ReorderLevel,
        },
        _ => null,
    };
}
