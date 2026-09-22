namespace OrderCore.Api.Modules.Orders.Domain.Entities;

/// <summary>
/// A line item of an <see cref="Order"/>. The unit price is snapshotted at
/// the moment the item is added and never recalculated from the product's
/// current price — an order must keep representing the original
/// transaction even if the catalog price changes later (section 9).
/// </summary>
public sealed class OrderItem
{
    public Guid ProductId { get; }

    public string ProductName { get; }

    public decimal UnitPrice { get; }

    public int Quantity { get; private set; }

    public decimal Total => UnitPrice * Quantity;

    internal OrderItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        Quantity += quantity;
    }
}
