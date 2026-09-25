using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Domain.Entities;

/// <summary>
/// Stock balance for a product (optionally a specific variant) —
/// 04-inventory.md. Reserving/releasing/consuming stock always goes
/// through this aggregate; nothing outside it ever does a bare
/// <c>stock -= quantity</c> (section 12 of the project context).
/// <see cref="QuantityAvailable"/> is computed, not stored — same
/// treatment as <c>Order.TotalAmount</c>.
/// </summary>
public sealed class StockItem : AggregateRoot<Guid>
{
    /// <summary>Longest reason a receipt or adjustment can carry into the movement history.</summary>
    public const int MaxReasonLength = 500;

    public Guid ProductId { get; private set; }

    public Guid? ProductVariantId { get; private set; }

    public int QuantityOnHand { get; private set; }

    public int QuantityReserved { get; private set; }

    public int QuantityAvailable => QuantityOnHand - QuantityReserved;

    /// <summary>
    /// Threshold used to flag low stock, set per item by an admin
    /// (<see cref="SetReorderLevel"/>, backoffice decision 4). Starts at 0,
    /// which never flags anything.
    /// </summary>
    public int ReorderLevel { get; private set; }

    /// <summary>
    /// Some units are still available but no more than <see cref="ReorderLevel"/>.
    /// An empty item is out of stock, not low, so it is excluded. With the
    /// default <see cref="ReorderLevel"/> of 0 this is always <c>false</c>.
    /// </summary>
    public bool IsLowStock => QuantityAvailable > 0 && QuantityAvailable <= ReorderLevel;

    public DateTimeOffset UpdatedAt { get; private set; }

    private StockItem()
    {
    }

    private StockItem(Guid id, Guid productId, Guid? productVariantId, int initialQuantity, DateTimeOffset now) : base(id)
    {
        ProductId = productId;
        ProductVariantId = productVariantId;
        QuantityOnHand = initialQuantity;
        UpdatedAt = now;
    }

    /// <summary>
    /// <paramref name="now"/> is not in 04-inventory.md's Create signature,
    /// but <see cref="UpdatedAt"/> needs a value from somewhere — same
    /// reasoning as <c>Customer.Create</c>'s `now` parameter.
    /// </summary>
    public static StockItem Create(Guid productId, int initialQuantity, Guid? productVariantId, DateTimeOffset now)
    {
        if (initialQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialQuantity), "Initial quantity cannot be negative.");
        }

        var stockItem = new StockItem(Guid.NewGuid(), productId, productVariantId, initialQuantity, now);
        stockItem.IncrementVersion();

        if (initialQuantity > 0)
        {
            stockItem.RecordMovement(StockMovementType.Inbound, initialQuantity, reason: null, now);
        }

        return stockItem;
    }

    /// <summary>New units arrived (a delivery from a supplier).</summary>
    public void Receive(int quantity, string? reason, DateTimeOffset now)
    {
        RequirePositive(quantity);
        RequireReasonWithinLimit(reason);

        QuantityOnHand += quantity;
        UpdatedAt = now;
        IncrementVersion();
        RecordMovement(StockMovementType.Inbound, quantity, reason, now);
    }

    /// <summary>
    /// Puts back on hand units a cancelled order had already consumed.
    /// Records no movement itself: the reservation being returned does
    /// (<see cref="InventoryReservation.Return"/>).
    /// </summary>
    public void ReturnConsumed(int quantity, DateTimeOffset now)
    {
        RequirePositive(quantity);

        QuantityOnHand += quantity;
        UpdatedAt = now;
        IncrementVersion();
    }

    public void SetReorderLevel(int reorderLevel, DateTimeOffset now)
    {
        if (reorderLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reorderLevel), "Reorder level cannot be negative.");
        }

        ReorderLevel = reorderLevel;
        UpdatedAt = now;
        IncrementVersion();
    }

    /// <summary>
    /// Returns <c>false</c> instead of throwing when there isn't enough
    /// available stock — that is the whole point of the <c>bool</c> return
    /// in 04-inventory.md: callers (section 11's central race) check the
    /// result rather than catching an exception per contended request.
    /// </summary>
    public bool TryReserve(int quantity)
    {
        RequirePositive(quantity);

        if (QuantityAvailable < quantity)
        {
            return false;
        }

        QuantityReserved += quantity;
        IncrementVersion();
        return true;
    }

    public void Release(int quantity)
    {
        RequirePositive(quantity);

        if (quantity > QuantityReserved)
        {
            throw new DomainRuleViolationException("invalid_stock_operation", "Cannot release more than is currently reserved.");
        }

        QuantityReserved -= quantity;
        IncrementVersion();
    }

    public void Consume(int quantity)
    {
        RequirePositive(quantity);

        if (quantity > QuantityReserved)
        {
            throw new DomainRuleViolationException("invalid_stock_operation", "Cannot consume more than is currently reserved.");
        }

        QuantityReserved -= quantity;
        QuantityOnHand -= quantity;
        IncrementVersion();
    }

    public void Adjust(int quantity, string reason, DateTimeOffset now)
    {
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Adjustment quantity cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required.", nameof(reason));
        }

        RequireReasonWithinLimit(reason);

        var newQuantityOnHand = QuantityOnHand + quantity;
        if (newQuantityOnHand < QuantityReserved)
        {
            throw new DomainRuleViolationException("stock_below_reserved", "Adjustment would leave fewer units on hand than are currently reserved.");
        }

        QuantityOnHand = newQuantityOnHand;
        UpdatedAt = now;
        IncrementVersion();
        RecordMovement(StockMovementType.Adjustment, quantity, reason, now);
    }

    private void RecordMovement(StockMovementType type, int quantity, string? reason, DateTimeOffset now) =>
        Raise(new InventoryStockMovementRecorded(
            Guid.NewGuid(), now, ProductId, type, quantity, nameof(StockItem), Id, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));

    private static void RequireReasonWithinLimit(string? reason)
    {
        if (reason is not null && reason.Trim().Length > MaxReasonLength)
        {
            throw new ArgumentException($"A reason can have at most {MaxReasonLength} characters.", nameof(reason));
        }
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }
    }

    /// <summary>
    /// Reconstructs a <see cref="StockItem"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static StockItem Rehydrate(
        Guid id,
        Guid productId,
        Guid? productVariantId,
        int quantityOnHand,
        int quantityReserved,
        int reorderLevel,
        DateTimeOffset updatedAt,
        int version)
    {
        return new StockItem(id, productId, productVariantId, quantityOnHand, updatedAt)
        {
            QuantityReserved = quantityReserved,
            ReorderLevel = reorderLevel,
            Version = version,
        };
    }
}
