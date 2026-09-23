namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

public sealed class ProductImagePersistenceModel
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? AltText { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPrimary { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ProductPersistenceModel Product { get; set; } = null!;
}
