using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Domain;

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

    public int Quantity { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset ReservedAt { get; private set; }

    private InventoryReservation()
    {
    }

    private InventoryReservation(Guid id, Guid productId, Guid orderId, int quantity, DateTimeOffset reservedAt)
        : base(id)
    {
        ProductId = productId;
        OrderId = orderId;
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
    public static InventoryReservation Create(Guid productId, Guid orderId, int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var reservation = new InventoryReservation(Guid.NewGuid(), productId, orderId, quantity, now);
        reservation.IncrementVersion();
        return reservation;
    }

    public void Release()
    {
        EnsureStatus(ReservationStatus.Reserved);
        Status = ReservationStatus.Released;
        IncrementVersion();
    }

    public void Consume()
    {
        EnsureStatus(ReservationStatus.Reserved);
        Status = ReservationStatus.Consumed;
        IncrementVersion();
    }

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
            throw new InvalidOperationException(
                $"Cannot transition reservation '{Id}' from '{Status}' as if it were '{expected}'.");
        }
    }
}
