namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

/// <summary>
/// 03-catalog.md's abbreviated version of this model only lists a handful
/// of fields; expanded here to every <see cref="Domain.Entities.Product"/>
/// field so <c>ProductMapper</c> can actually round-trip a valid Product
/// back out of the database — same reasoning as
/// CustomerAddressPersistenceModel in 02-customers.md.
/// </summary>
public sealed class ProductPersistenceModel
{
    public Guid Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public Guid CategoryId { get; set; }

    public string? Brand { get; set; }

    public decimal CurrentPrice { get; set; }

    public decimal? CompareAtPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    public int? WeightGrams { get; set; }

    public decimal? HeightCm { get; set; }

    public decimal? WidthCm { get; set; }

    public decimal? DepthCm { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool Active { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }

    public ICollection<ProductImagePersistenceModel> Images { get; set; } = new List<ProductImagePersistenceModel>();

    public ICollection<ProductVariantPersistenceModel> Variants { get; set; } = new List<ProductVariantPersistenceModel>();
}
