using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Domain.Events;

/// <summary>
/// Raised for every change to a product's stock, and dispatched through
/// <c>IDomainEventDispatcher</c> after a successful save, where
/// <c>StockMovementRecorder</c> turns it into the product's movement
/// history. Reservations raise it on Create/Release/Consume/Return
/// (referencing the reservation); a <see cref="Entities.StockItem"/>
/// raises it on receiving and adjusting stock (referencing itself), with
/// the admin's <see cref="Reason"/>. See
/// Docs/specs/inventory/stock-and-reservations.md for why this is a single
/// event type parametrized by <see cref="MovementType"/> rather than one
/// per movement. <see cref="Quantity"/> is signed for adjustments.
/// Reservation movements carry the reservation's <see cref="OrderId"/>,
/// which the integration events published for them need.
/// </summary>
public sealed record InventoryStockMovementRecorded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    StockMovementType MovementType,
    int Quantity,
    string ReferenceType,
    Guid ReferenceId,
    string? Reason = null,
    Guid? OrderId = null) : IDomainEvent;

/// <summary>
/// A <see cref="Entities.StockItem"/> has just entered a state worth
/// warning about — low on stock or out of it. Raised on the change of
/// state only, never again while the item stays there, so each alert is
/// news (Docs/specs/events/async-messaging.md: stock alerts are published,
/// with no consumer yet).
/// </summary>
public sealed record StockAlertRaised(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    StockAlertLevel Level,
    int QuantityAvailable,
    int ReorderLevel) : IDomainEvent;
