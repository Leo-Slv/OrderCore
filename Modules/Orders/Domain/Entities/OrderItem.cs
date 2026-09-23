namespace OrderCore.Api.Modules.Orders.Domain.Entities;

/// <summary>
/// A line item of an <see cref="Order"/>. The unit price is snapshotted at
/// the moment the item is added and never recalculated from the product's
/// current price — an order must keep representing the original
/// transaction even if the catalog price changes later (section 9).
/// <see cref="DiscountAmount"/> only ever changes through
/// <see cref="ApplyDiscount"/> — see 05-orders.md's "Comportamento de
/// OrderItem".
/// </summary>
public sealed class OrderItem
{
    public Guid ProductId { get; }

    public Guid? ProductVariantId { get; }

    public string ProductSku { get; }

    public string ProductName { get; }

    public string? ProductImageUrl { get; }

    public decimal UnitPrice { get; }

    public int Quantity { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal Total => (UnitPrice * Quantity) - DiscountAmount;

    internal OrderItem(
        Guid productId,
        Guid? productVariantId,
        string productSku,
        string productName,
        string? productImageUrl,
        decimal unitPrice,
        int quantity)
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
        ProductVariantId = productVariantId;
        ProductSku = productSku;
        ProductName = productName;
        ProductImageUrl = productImageUrl;
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

    internal void DecreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (quantity >= Quantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "Cannot decrease quantity to zero or below — remove the item instead.");
        }

        Quantity -= quantity;
    }

    internal void ApplyDiscount(decimal amount)
    {
        if (amount < 0 || amount > UnitPrice * Quantity)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Discount must be between 0 and the item's full price.");
        }

        DiscountAmount = amount;
    }
}
