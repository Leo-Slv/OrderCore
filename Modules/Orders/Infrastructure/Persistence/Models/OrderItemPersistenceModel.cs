namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

/// <summary>
/// Keyed by (OrderId, ProductId), not a surrogate Id: <c>OrderItem</c> has
/// no identity of its own in the domain (<c>Order.AddItem</c> enforces one
/// row per product per order), so there is nothing else to key it by.
/// Expanded beyond 05-orders.md's abbreviated shape with
/// <see cref="ProductVariantId"/>/<see cref="ProductSku"/>/
/// <see cref="ProductImageUrl"/>/<see cref="DiscountAmount"/>, same
/// reasoning as <c>OrderPersistenceModel</c>.
/// </summary>
public sealed class OrderItemPersistenceModel
{
    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public string ProductSku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? ProductImageUrl { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal DiscountAmount { get; set; }

    public OrderPersistenceModel Order { get; set; } = null!;
}
