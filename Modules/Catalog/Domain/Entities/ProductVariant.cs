using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Catalog.Domain.Entities;

/// <summary>
/// A variation of a <see cref="Product"/> (e.g. size/color). Can be
/// deactivated independently of the parent product — see 03-catalog.md's
/// "Comportamento das entidades filhas". <see cref="AttributesJson"/> stays
/// a raw string for now; worth a typed accessor if consumers start reading
/// specific attributes out of it.
/// </summary>
public sealed class ProductVariant : Entity<Guid>
{
    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string AttributesJson { get; private set; } = string.Empty;

    public decimal AdditionalPrice { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private ProductVariant()
    {
    }

    private ProductVariant(Guid id, string sku, string name, string attributesJson, decimal additionalPrice, DateTimeOffset now)
        : base(id)
    {
        Sku = sku;
        Name = name;
        AttributesJson = attributesJson;
        AdditionalPrice = additionalPrice;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static ProductVariant Create(string sku, string name, string attributesJson, decimal additionalPrice, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("Sku is required.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new ProductVariant(Guid.NewGuid(), sku, name, attributesJson, additionalPrice, now);
    }

    public void ChangeAdditionalPrice(decimal newAdditionalPrice)
    {
        AdditionalPrice = newAdditionalPrice;
    }

    public void Activate() => Active = true;

    public void Deactivate() => Active = false;

    /// <summary>
    /// Reconstructs a <see cref="ProductVariant"/> from already-persisted
    /// state, distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static ProductVariant Rehydrate(
        Guid id, string sku, string name, string attributesJson, decimal additionalPrice, bool active, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        return new ProductVariant(id, sku, name, attributesJson, additionalPrice, createdAt)
        {
            Active = active,
            UpdatedAt = updatedAt,
        };
    }
}
