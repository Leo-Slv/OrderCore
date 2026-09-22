using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Catalog.Domain.Entities;

/// <summary>
/// Minimal Catalog aggregate. <see cref="CurrentPrice"/> is the price used
/// when a new order item is created — it must never be used to recalculate
/// the value of an existing (historical) order (section 9).
/// </summary>
public sealed class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public decimal CurrentPrice { get; private set; }

    private Product()
    {
    }

    private Product(Guid id, string name, decimal currentPrice) : base(id)
    {
        Name = name;
        CurrentPrice = currentPrice;
    }

    public static Product Create(string name, decimal currentPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (currentPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentPrice), "Price cannot be negative.");
        }

        var product = new Product(Guid.NewGuid(), name, currentPrice);
        product.IncrementVersion();
        return product;
    }

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Price cannot be negative.");
        }

        CurrentPrice = newPrice;
        IncrementVersion();
    }
}
