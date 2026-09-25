using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Domain.Entities;

/// <summary>
/// Main aggregate of the system (section 9). Owns the invariants of an
/// order and its items, and controls every state transition explicitly
/// (section 10) — there is no public setter for <see cref="Status"/>.
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    public const int MaxInternalNotesLength = 2000;

    public const int MaxCheckoutIdempotencyKeyLength = 100;

    private readonly List<OrderItem> _items = new();

    public string OrderNumber { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal SubtotalAmount => _items.Sum(i => i.Total);

    public decimal DiscountAmount { get; private set; }

    public decimal ShippingAmount { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal TotalAmount => SubtotalAmount - DiscountAmount + ShippingAmount + TaxAmount;

    public string Currency { get; private set; } = "BRL";

    public Address? ShippingAddress { get; private set; }

    public Address? BillingAddress { get; private set; }

    public string? CustomerNotes { get; private set; }

    public string? InternalNotes { get; private set; }

    /// <summary>
    /// Client-supplied key of the checkout request that created this order,
    /// unique per customer. Replaying the same checkout returns this order
    /// instead of creating another. Null for orders created any other way.
    /// </summary>
    public string? CheckoutIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset? ShippedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    // Required by EF Core.
    private Order()
    {
    }

    private Order(Guid id, Guid customerId, string currency, string orderNumber, DateTimeOffset createdAt) : base(id)
    {
        CustomerId = customerId;
        Currency = currency;
        OrderNumber = orderNumber;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = OrderStatus.Created;
    }

    /// <summary>
    /// <paramref name="customerNotes"/> is not in 05-orders.md's Create
    /// signature, but no other method ever sets <see cref="CustomerNotes"/>
    /// either — same class of gap as <c>Category.Create</c> gaining
    /// `description`.
    /// </summary>
    public static Order Create(
        Guid customerId,
        string currency,
        string orderNumber,
        DateTimeOffset now,
        string? customerNotes = null,
        string? checkoutIdempotencyKey = null)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("Order number is required.", nameof(orderNumber));
        }

        if (checkoutIdempotencyKey is not null
            && (string.IsNullOrWhiteSpace(checkoutIdempotencyKey) || checkoutIdempotencyKey.Length > MaxCheckoutIdempotencyKeyLength))
        {
            throw new ArgumentException(
                $"A checkout idempotency key must be non-blank and at most {MaxCheckoutIdempotencyKeyLength} characters.",
                nameof(checkoutIdempotencyKey));
        }

        var order = new Order(Guid.NewGuid(), customerId, currency, orderNumber, now)
        {
            CustomerNotes = customerNotes,
            CheckoutIdempotencyKey = checkoutIdempotencyKey,
        };
        order.IncrementVersion();
        order.Raise(new OrderCreated(Guid.NewGuid(), now, order.Id, customerId));
        return order;
    }

    /// <summary>
    /// Adds a product to the order, snapshotting its current price. Only
    /// allowed while the order has not yet moved past <see cref="OrderStatus.Created"/>,
    /// so that a pending/confirmed order cannot be silently altered.
    /// </summary>
    public void AddItem(
        Guid productId,
        Guid? productVariantId,
        string productSku,
        string productName,
        string? productImageUrl,
        decimal unitPrice,
        int quantity)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot add items to an order in status '{Status}'.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(productId, productVariantId, productSku, productName, productImageUrl, unitPrice, quantity));
        }

        IncrementVersion();
    }

    public void RemoveItem(Guid productId)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot remove items from an order in status '{Status}'.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainRuleViolationException("order_item_not_found", $"Product '{productId}' is not part of this order.");

        _items.Remove(existing);
        IncrementVersion();
    }

    /// <summary>
    /// Not in 05-orders.md's method list: <c>OrderItem.DecreaseQuantity</c>
    /// is `internal` (only <see cref="Order"/> can call it, same as
    /// <c>IncreaseQuantity</c> already was), so the aggregate needs its own
    /// pass-through to actually reach it from outside.
    /// </summary>
    public void DecreaseItemQuantity(Guid productId, int quantity)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot change item quantities on an order in status '{Status}'.");

        var existing = FindItem(productId);
        existing.DecreaseQuantity(quantity);
        IncrementVersion();
    }

    /// <summary>
    /// Not in 05-orders.md's method list — see
    /// <see cref="DecreaseItemQuantity"/>'s remarks; same reasoning for
    /// <c>OrderItem.ApplyDiscount</c>.
    /// </summary>
    public void ApplyItemDiscount(Guid productId, decimal amount)
    {
        var existing = FindItem(productId);
        existing.ApplyDiscount(amount);
        IncrementVersion();
    }

    private OrderItem FindItem(Guid productId) =>
        _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainRuleViolationException("order_item_not_found", $"Product '{productId}' is not part of this order.");

    public void SetAddresses(Address shippingAddress, Address billingAddress)
    {
        ArgumentNullException.ThrowIfNull(shippingAddress);
        ArgumentNullException.ThrowIfNull(billingAddress);

        ShippingAddress = shippingAddress;
        BillingAddress = billingAddress;
        IncrementVersion();
    }

    /// <summary>Staff-only notes, never shown to the customer. Blank clears them.</summary>
    public void SetInternalNotes(string? notes)
    {
        if (notes?.Length > MaxInternalNotesLength)
        {
            throw new ArgumentException($"Internal notes can have at most {MaxInternalNotesLength} characters.", nameof(notes));
        }

        InternalNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IncrementVersion();
    }

    public void ApplyDiscount(decimal amount)
    {
        if (amount < 0 || amount > SubtotalAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Discount must be between 0 and the order's subtotal.");
        }

        DiscountAmount = amount;
        IncrementVersion();
    }

    public void SetShippingAmount(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Shipping amount cannot be negative.");
        }

        ShippingAmount = amount;
        IncrementVersion();
    }

    public void SetTaxAmount(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Tax amount cannot be negative.");
        }

        TaxAmount = amount;
        IncrementVersion();
    }

    /// <summary>
    /// Moves the order from Created to PendingPayment once inventory has
    /// been successfully reserved for every item (section 12).
    /// </summary>
    public void RequestPayment(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot request payment for an order in status '{Status}'.");

        if (_items.Count == 0)
        {
            throw new DomainRuleViolationException("order_without_items", "Cannot request payment for an order with no items.");
        }

        Status = OrderStatus.PendingPayment;
        IncrementVersion();
        Raise(new OrderPaymentRequested(Guid.NewGuid(), now, Id));
    }

    public void Confirm(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.PendingPayment, $"Cannot confirm an order in status '{Status}'.");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = now;
        IncrementVersion();
        Raise(new OrderConfirmed(Guid.NewGuid(), now, Id));
    }

    public void StartProcessing(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Confirmed, $"Cannot start processing an order in status '{Status}'.");

        Status = OrderStatus.Processing;
        IncrementVersion();
        Raise(new OrderProcessingStarted(Guid.NewGuid(), now, Id));
    }

    /// <summary>
    /// Checked before shipping's side effect (capturing the payment), so a
    /// payment is never captured for an order that can't be shipped.
    /// </summary>
    public void EnsureCanShip() =>
        EnsureStatus(OrderStatus.Processing, $"Cannot ship an order in status '{Status}'.");

    public void Ship(DateTimeOffset now)
    {
        EnsureCanShip();

        Status = OrderStatus.Shipped;
        ShippedAt = now;
        IncrementVersion();
        Raise(new OrderShipped(Guid.NewGuid(), now, Id));
    }

    public void Deliver(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Shipped, $"Cannot deliver an order in status '{Status}'.");

        Status = OrderStatus.Delivered;
        DeliveredAt = now;
        IncrementVersion();
        Raise(new OrderDelivered(Guid.NewGuid(), now, Id));
    }

    public void FailPayment(string reason, DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.PendingPayment, $"Cannot fail payment for an order in status '{Status}'.");

        Status = OrderStatus.PaymentFailed;
        IncrementVersion();
        Raise(new OrderPaymentFailed(Guid.NewGuid(), now, Id, reason));
    }

    /// <summary>
    /// Checked before cancelling's side effects (settling the payment,
    /// returning stock), so they never happen for an order that can't be
    /// cancelled. Same rule <see cref="Cancel"/> applies.
    /// </summary>
    public void EnsureCanBeCancelled()
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Shipped or OrderStatus.Cancelled)
        {
            throw new DomainRuleViolationException("invalid_order_state", $"Cannot cancel an order in status '{Status}'.");
        }
    }

    /// <summary>
    /// Cancellation is only allowed from states where no irreversible
    /// fulfillment step has happened yet. In particular, a Delivered order
    /// can never transition back to any earlier state (section 10).
    /// </summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        EnsureCanBeCancelled();

        Status = OrderStatus.Cancelled;
        CancelledAt = now;
        IncrementVersion();
        Raise(new OrderCancelled(Guid.NewGuid(), now, Id, reason));
    }

    private void EnsureStatus(OrderStatus expected, string errorMessage)
    {
        if (Status != expected)
        {
            throw new DomainRuleViolationException("invalid_order_state", errorMessage);
        }
    }

    /// <summary>
    /// Reconstructs an <see cref="Order"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module) — loading an
    /// existing order must never re-raise <c>OrderCreated</c>.
    /// </summary>
    internal static Order Rehydrate(
        Guid id,
        string orderNumber,
        Guid customerId,
        OrderStatus status,
        decimal discountAmount,
        decimal shippingAmount,
        decimal taxAmount,
        string currency,
        Address? shippingAddress,
        Address? billingAddress,
        string? customerNotes,
        string? internalNotes,
        string? checkoutIdempotencyKey,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? confirmedAt,
        DateTimeOffset? cancelledAt,
        DateTimeOffset? shippedAt,
        DateTimeOffset? deliveredAt,
        int version,
        IEnumerable<OrderItem> items)
    {
        var order = new Order(id, customerId, currency, orderNumber, createdAt)
        {
            DiscountAmount = discountAmount,
            ShippingAmount = shippingAmount,
            TaxAmount = taxAmount,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress,
            CustomerNotes = customerNotes,
            InternalNotes = internalNotes,
            CheckoutIdempotencyKey = checkoutIdempotencyKey,
            UpdatedAt = updatedAt,
            Status = status,
            ConfirmedAt = confirmedAt,
            CancelledAt = cancelledAt,
            ShippedAt = shippedAt,
            DeliveredAt = deliveredAt,
            Version = version,
        };

        order._items.AddRange(items);

        return order;
    }
}
