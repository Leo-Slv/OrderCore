using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.InventoryReservation"/> on
/// Create/Release/Consume — the three operations 04-inventory.md wires to
/// <c>StockMovementRecorder</c> — and dispatched through
/// <c>IDomainEventDispatcher</c> after a successful save. See
/// Docs/specs/inventory/stock-and-reservations.md for why this is a single
/// event type parametrized by <see cref="MovementType"/> rather than three.
/// </summary>
public sealed record InventoryStockMovementRecorded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid ProductId,
    StockMovementType MovementType,
    int Quantity,
    string ReferenceType,
    Guid ReferenceId) : IDomainEvent;
