namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;

/// <summary>
/// Expanded beyond 03-catalog.md's abbreviated Id/Name/Slug/ParentCategoryId/Active
/// shape to every <see cref="Domain.Entities.Category"/> field (Description,
/// DisplayOrder, CreatedAt, UpdatedAt, Version) for the same reason
/// <see cref="ProductPersistenceModel"/> is.
/// </summary>
public sealed class CategoryPersistenceModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool Active { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
