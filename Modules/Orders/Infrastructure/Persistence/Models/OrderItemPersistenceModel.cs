namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

/// <summary>
/// Keyed by (OrderId, ProductId), not a surrogate Id: <c>OrderItem</c> has
/// no identity of its own in the domain (<c>Order.AddItem</c> enforces one
/// row per product per order), so there is nothing else to key it by.
/// </summary>
public sealed class OrderItemPersistenceModel
{
    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public OrderPersistenceModel Order { get; set; } = null!;
}
