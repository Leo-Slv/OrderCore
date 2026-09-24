using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Domain.Entities;

/// <summary>
/// Explicit reservation of stock for an order item (section 12 of the
/// project context). Reserving stock is never a bare "stock -= quantity";
/// it is a first-class concept that can later be released (payment
/// failed), consumed (order fulfilled) or expired (reservation timed out),
/// which is what makes compensation flows possible.
/// </summary>
public sealed class InventoryReservation : AggregateRoot<Guid>
{
    public Guid ProductId { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid OrderItemId { get; private set; }

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset ReservedAt { get; private set; }

    /// <summary>
    /// Always null for now: nothing in 04-inventory.md sets it (no
    /// expiration policy/duration is specified anywhere) or reads it (no
    /// scheduled job auto-expiring reservations exists yet) — same
    /// treatment as <c>StockItem.ReorderLevel</c>.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? ReleasedAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    private InventoryReservation()
    {
    }

    private InventoryReservation(Guid id, Guid productId, Guid orderId, Guid orderItemId, int quantity, DateTimeOffset reservedAt)
        : base(id)
    {
        ProductId = productId;
        OrderId = orderId;
        OrderItemId = orderItemId;
        Quantity = quantity;
        ReservedAt = reservedAt;
        Status = ReservationStatus.Reserved;
    }

    /// <summary>
    /// Factory used by the Inventory module after it has atomically
    /// confirmed, at the persistence level (optimistic concurrency /
    /// unique constraint — see ADR-009), that enough stock was available.
    /// This type does not itself decide whether stock is available; that
    /// is a concern of the Inventory aggregate / application service,
    /// which must guard against the race condition described in section 11
    /// ("Stock = 1, Request A and Request B reserve concurrently").
    /// </summary>
    public static InventoryReservation Create(Guid productId, Guid orderId, Guid orderItemId, int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var reservation = new InventoryReservation(Guid.NewGuid(), productId, orderId, orderItemId, quantity, now);
        reservation.IncrementVersion();
        reservation.Raise(new InventoryStockMovementRecorded(
            Guid.NewGuid(), now, productId, StockMovementType.ReservationCreated, quantity, nameof(InventoryReservation), reservation.Id));
        return reservation;
    }

    /// <summary>
    /// <paramref name="now"/> is not in 04-inventory.md's abbreviated
    /// signature; added per the diagram's own "Paridade" note so
    /// <see cref="ReleasedAt"/> can actually be filled in.
    /// </summary>
    public void Release(DateTimeOffset now)
    {
        EnsureStatus(ReservationStatus.Reserved);
        Status = ReservationStatus.Released;
        ReleasedAt = now;
        IncrementVersion();
        Raise(new InventoryStockMovementRecorded(
            Guid.NewGuid(), now, ProductId, StockMovementType.ReservationReleased, Quantity, nameof(InventoryReservation), Id));
    }

    /// <summary>
    /// <paramref name="now"/> — see <see cref="Release"/>'s remarks.
    /// </summary>
    public void Consume(DateTimeOffset now)
    {
        EnsureStatus(ReservationStatus.Reserved);
        Status = ReservationStatus.Consumed;
        ConsumedAt = now;
        IncrementVersion();
        Raise(new InventoryStockMovementRecorded(
            Guid.NewGuid(), now, ProductId, StockMovementType.ReservationConsumed, Quantity, nameof(InventoryReservation), Id));
    }

    /// <summary>
    /// No `now` parameter: the diagram's own "Paridade" note says there is
    /// no separate `expired_at` column — the `Expired` status is enough.
    /// </summary>
    public void Expire()
    {
        EnsureStatus(ReservationStatus.Reserved);
        Status = ReservationStatus.Expired;
        IncrementVersion();
    }

    private void EnsureStatus(ReservationStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainRuleViolationException(
                "invalid_reservation_state",
                $"Cannot transition reservation '{Id}' from '{Status}' as if it were '{expected}'.");
        }
    }

    /// <summary>
    /// Reconstructs an <see cref="InventoryReservation"/> from
    /// already-persisted state, distinct from <see cref="Create"/> the
    /// same way <c>Customer.Rehydrate</c> is (Shared kernel module) —
    /// loading an existing reservation must never re-raise
    /// <see cref="InventoryStockMovementRecorded"/>.
    /// </summary>
    internal static InventoryReservation Rehydrate(
        Guid id,
        Guid productId,
        Guid orderId,
        Guid orderItemId,
        int quantity,
        ReservationStatus status,
        DateTimeOffset reservedAt,
        DateTimeOffset? expiresAt,
        DateTimeOffset? releasedAt,
        DateTimeOffset? consumedAt,
        int version)
    {
        return new InventoryReservation(id, productId, orderId, orderItemId, quantity, reservedAt)
        {
            Status = status,
            ExpiresAt = expiresAt,
            ReleasedAt = releasedAt,
            ConsumedAt = consumedAt,
            Version = version,
        };
    }
}
