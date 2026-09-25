using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Catalog.Domain.Entities;

/// <summary>
/// One of a <see cref="Product"/>'s images. Primary-image promotion is
/// handled through <see cref="MarkAsPrimary"/>/<see cref="UnmarkAsPrimary"/>
/// so <c>Product.AddImage</c> never flips <see cref="IsPrimary"/> directly
/// — see 03-catalog.md's "Comportamento das entidades filhas".
/// </summary>
public sealed class ProductImage : Entity<Guid>
{
    public const int MaxUrlLength = 2000;

    public const int MaxAltTextLength = 200;

    public string Url { get; private set; } = string.Empty;

    public string? AltText { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private ProductImage()
    {
    }

    private ProductImage(Guid id, string url, string? altText, bool isPrimary, DateTimeOffset now) : base(id)
    {
        Url = url;
        AltText = altText;
        IsPrimary = isPrimary;
        CreatedAt = now;
    }

    public static ProductImage Create(string url, string? altText, bool isPrimary, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Url is required.", nameof(url));
        }

        if (url.Length > MaxUrlLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Url must be an absolute http(s) address of at most {MaxUrlLength} characters.", nameof(url));
        }

        if (altText?.Length > MaxAltTextLength)
        {
            throw new ArgumentException($"Alt text can have at most {MaxAltTextLength} characters.", nameof(altText));
        }

        return new ProductImage(Guid.NewGuid(), url, altText, isPrimary, now);
    }

    public void ChangeAltText(string? altText)
    {
        AltText = altText;
    }

    public void MarkAsPrimary() => IsPrimary = true;

    public void UnmarkAsPrimary() => IsPrimary = false;

    /// <summary>
    /// Set by <c>Product.ReorderImages</c> only — not part of
    /// 03-catalog.md's public API, `internal` for the same reason
    /// Mark/UnmarkAsPrimary stay out of the aggregate's own field access.
    /// </summary>
    internal void SetDisplayOrder(int order) => DisplayOrder = order;

    /// <summary>
    /// Reconstructs a <see cref="ProductImage"/> from already-persisted
    /// state, distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static ProductImage Rehydrate(Guid id, string url, string? altText, int displayOrder, bool isPrimary, DateTimeOffset createdAt)
    {
        var image = new ProductImage(id, url, altText, isPrimary, createdAt);
        image.SetDisplayOrder(displayOrder);
        return image;
    }
}
