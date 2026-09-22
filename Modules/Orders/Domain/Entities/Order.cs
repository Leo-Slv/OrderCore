using OrderCore.Api.Modules.Orders.Domain.Enums;
using OrderCore.Api.Modules.Orders.Domain.Events;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Orders.Domain.Entities;

/// <summary>
/// Main aggregate of the system (section 9). Owns the invariants of an
/// order and its items, and controls every state transition explicitly
/// (section 10) — there is no public setter for <see cref="Status"/>.
/// </summary>
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = new();

    public Guid CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal TotalAmount => _items.Sum(i => i.Total);

    public string Currency { get; private set; } = "BRL";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    // Required by EF Core.
    private Order()
    {
    }

    private Order(Guid id, Guid customerId, string currency, DateTimeOffset createdAt) : base(id)
    {
        CustomerId = customerId;
        Currency = currency;
        CreatedAt = createdAt;
        Status = OrderStatus.Created;
    }

    public static Order Create(Guid customerId, string currency, DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        var order = new Order(Guid.NewGuid(), customerId, currency, now);
        order.IncrementVersion();
        order.Raise(new OrderCreated(Guid.NewGuid(), now, order.Id, customerId));
        return order;
    }

    /// <summary>
    /// Adds a product to the order, snapshotting its current price. Only
    /// allowed while the order has not yet moved past <see cref="OrderStatus.Created"/>,
    /// so that a pending/confirmed order cannot be silently altered.
    /// </summary>
    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot add items to an order in status '{Status}'.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(productId, productName, unitPrice, quantity));
        }

        IncrementVersion();
    }

    public void RemoveItem(Guid productId)
    {
        EnsureStatus(OrderStatus.Created, $"Cannot remove items from an order in status '{Status}'.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new InvalidOperationException($"Product '{productId}' is not part of this order.");

        _items.Remove(existing);
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
            throw new InvalidOperationException("Cannot request payment for an order with no items.");
        }

        Status = OrderStatus.PendingPayment;
        IncrementVersion();
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
    }

    public void Ship(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Processing, $"Cannot ship an order in status '{Status}'.");

        Status = OrderStatus.Shipped;
        IncrementVersion();
    }

    public void Deliver(DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.Shipped, $"Cannot deliver an order in status '{Status}'.");

        Status = OrderStatus.Delivered;
        IncrementVersion();
    }

    public void FailPayment(string reason, DateTimeOffset now)
    {
        EnsureStatus(OrderStatus.PendingPayment, $"Cannot fail payment for an order in status '{Status}'.");

        Status = OrderStatus.PaymentFailed;
        IncrementVersion();
        Raise(new OrderPaymentFailed(Guid.NewGuid(), now, Id, reason));
    }

    /// <summary>
    /// Cancellation is only allowed from states where no irreversible
    /// fulfillment step has happened yet. In particular, a Delivered order
    /// can never transition back to any earlier state (section 10).
    /// </summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Shipped or OrderStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel an order in status '{Status}'.");
        }

        Status = OrderStatus.Cancelled;
        CancelledAt = now;
        IncrementVersion();
        Raise(new OrderCancelled(Guid.NewGuid(), now, Id, reason));
    }

    private void EnsureStatus(OrderStatus expected, string errorMessage)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
