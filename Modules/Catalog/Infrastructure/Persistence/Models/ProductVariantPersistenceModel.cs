namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

public sealed class ProductVariantPersistenceModel
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string AttributesJson { get; set; } = string.Empty;

    public decimal AdditionalPrice { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ProductPersistenceModel Product { get; set; } = null!;
}
