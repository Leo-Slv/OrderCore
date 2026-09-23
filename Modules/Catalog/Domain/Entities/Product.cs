using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Modules.Catalog.Domain.Events;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Domain.Entities;

/// <summary>
/// Catalog aggregate (03-catalog.md). <see cref="CurrentPrice"/> is the
/// price used when a new order item is created — it must never be used to
/// recalculate the value of an existing (historical) order (section 9).
/// </summary>
public sealed class Product : AggregateRoot<Guid>
{
    private readonly List<ProductImage> _images = new();
    private readonly List<ProductVariant> _variants = new();

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Slug Slug { get; private set; } = null!;

    public string? ShortDescription { get; private set; }

    public string? Description { get; private set; }

    public Guid CategoryId { get; private set; }

    public string? Brand { get; private set; }

    public decimal CurrentPrice { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    public string Currency { get; private set; } = "BRL";

    public int? WeightGrams { get; private set; }

    public decimal? HeightCm { get; private set; }

    public decimal? WidthCm { get; private set; }

    public decimal? DepthCm { get; private set; }

    public ProductStatus Status { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    private Product()
    {
    }

    private Product(Guid id, string sku, string name, Slug slug, Guid categoryId, decimal currentPrice, string currency, DateTimeOffset now)
        : base(id)
    {
        Sku = sku;
        Name = name;
        Slug = slug;
        CategoryId = categoryId;
        CurrentPrice = currentPrice;
        Currency = currency;
        Status = ProductStatus.Draft;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Product Create(
        string sku, string name, Slug slug, Guid categoryId, decimal currentPrice, string currency, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("Sku is required.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(slug);

        if (currentPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentPrice), "Price cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        var product = new Product(Guid.NewGuid(), sku, name, slug, categoryId, currentPrice, currency, now);
        product.IncrementVersion();
        product.Raise(new ProductCreated(Guid.NewGuid(), now, product.Id));
        return product;
    }

    /// <summary>
    /// 03-catalog.md's signature has no `now`, but raising
    /// <see cref="ProductPriceChanged"/> needs an OccurredAt — the domain
    /// must not read the clock itself (same reasoning as
    /// <c>Customer.Create</c>'s `now` parameter).
    /// </summary>
    public void ChangePrice(decimal newPrice, DateTimeOffset now)
    {
        if (newPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPrice), "Price cannot be negative.");
        }

        var oldPrice = CurrentPrice;
        CurrentPrice = newPrice;
        IncrementVersion();
        Raise(new ProductPriceChanged(Guid.NewGuid(), now, Id, oldPrice, newPrice));
    }

    public void UpdateDetails(string name, string? shortDescription, string? description, string? brand)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        ShortDescription = shortDescription;
        Description = description;
        Brand = brand;
        IncrementVersion();
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != ProductStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot publish a product in status '{Status}'.");
        }

        Status = ProductStatus.Active;
        PublishedAt = now;
        IncrementVersion();
        Raise(new ProductPublished(Guid.NewGuid(), now, Id));
    }

    public void Discontinue()
    {
        if (Status == ProductStatus.Discontinued)
        {
            throw new InvalidOperationException("Product is already discontinued.");
        }

        Status = ProductStatus.Discontinued;
        IncrementVersion();
    }

    /// <summary>
    /// 03-catalog.md's signature has no `now`, but <c>ProductImage.Create</c>
    /// needs one — matches the same class of gap already documented for
    /// <c>Customer.Create</c> in 02-customers.md.
    /// </summary>
    public void AddImage(string url, string? altText, bool isPrimary, DateTimeOffset now)
    {
        var image = ProductImage.Create(url, altText, isPrimary, now);

        if (isPrimary)
        {
            foreach (var existing in _images.Where(i => i.IsPrimary))
            {
                existing.UnmarkAsPrimary();
            }
        }

        _images.Add(image);
        IncrementVersion();
    }

    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new InvalidOperationException($"Image '{imageId}' does not belong to this product.");

        _images.Remove(image);
        IncrementVersion();
    }

    public void ReorderImages(IReadOnlyList<Guid> orderedImageIds)
    {
        for (var index = 0; index < orderedImageIds.Count; index++)
        {
            var image = _images.FirstOrDefault(i => i.Id == orderedImageIds[index])
                ?? throw new InvalidOperationException($"Image '{orderedImageIds[index]}' does not belong to this product.");

            image.SetDisplayOrder(index);
        }

        IncrementVersion();
    }

    /// <summary>
    /// 03-catalog.md's signature has no `now` — same gap as
    /// <see cref="AddImage"/>, needed by <c>ProductVariant.Create</c>.
    /// </summary>
    public void AddVariant(string sku, string name, string attributesJson, decimal additionalPrice, DateTimeOffset now)
    {
        var variant = ProductVariant.Create(sku, name, attributesJson, additionalPrice, now);
        _variants.Add(variant);
        IncrementVersion();
    }

    public void RemoveVariant(Guid variantId)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new InvalidOperationException($"Variant '{variantId}' does not belong to this product.");

        _variants.Remove(variant);
        IncrementVersion();
    }

    /// <summary>
    /// Reconstructs a <see cref="Product"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static Product Rehydrate(
        Guid id,
        string sku,
        string name,
        Slug slug,
        string? shortDescription,
        string? description,
        Guid categoryId,
        string? brand,
        decimal currentPrice,
        decimal? compareAtPrice,
        string currency,
        int? weightGrams,
        decimal? heightCm,
        decimal? widthCm,
        decimal? depthCm,
        ProductStatus status,
        bool active,
        DateTimeOffset? publishedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version,
        IEnumerable<ProductImage> images,
        IEnumerable<ProductVariant> variants)
    {
        var product = new Product(id, sku, name, slug, categoryId, currentPrice, currency, createdAt)
        {
            ShortDescription = shortDescription,
            Description = description,
            Brand = brand,
            CompareAtPrice = compareAtPrice,
            WeightGrams = weightGrams,
            HeightCm = heightCm,
            WidthCm = widthCm,
            DepthCm = depthCm,
            Status = status,
            Active = active,
            PublishedAt = publishedAt,
            UpdatedAt = updatedAt,
            Version = version,
        };

        product._images.AddRange(images);
        product._variants.AddRange(variants);

        return product;
    }
}
