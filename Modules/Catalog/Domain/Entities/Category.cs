using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Domain.Entities;

/// <summary>
/// Catalog category (03-catalog.md). Categories can nest under a parent
/// (<see cref="ParentCategoryId"/>), but this aggregate only stores the
/// parent's id — walking/validating a category tree is a concern for the
/// application layer, not the aggregate itself.
/// </summary>
public sealed class Category : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public Slug Slug { get; private set; } = null!;

    public Guid? ParentCategoryId { get; private set; }

    public string? Description { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool Active { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Category()
    {
    }

    private Category(Guid id, string name, Slug slug, Guid? parentCategoryId, string? description, DateTimeOffset now)
        : base(id)
    {
        Name = name;
        Slug = slug;
        ParentCategoryId = parentCategoryId;
        Description = description;
        Active = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// <paramref name="description"/> is not in 03-catalog.md's Create
    /// signature, but no other method sets <see cref="Description"/>
    /// either — without it the property could never hold a value.
    /// </summary>
    public static Category Create(string name, Slug slug, Guid? parentCategoryId, string? description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(slug);

        return new Category(Guid.NewGuid(), name, slug, parentCategoryId, description, now);
    }

    public void Rename(string name, Slug slug)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(slug);

        Name = name;
        Slug = slug;
        IncrementVersion();
    }

    public void ChangeDisplayOrder(int order)
    {
        DisplayOrder = order;
        IncrementVersion();
    }

    public void Activate()
    {
        Active = true;
        IncrementVersion();
    }

    public void Deactivate()
    {
        Active = false;
        IncrementVersion();
    }

    /// <summary>
    /// Reconstructs a <see cref="Category"/> from already-persisted state,
    /// distinct from <see cref="Create"/> the same way
    /// <c>Customer.Rehydrate</c> is (Shared kernel module).
    /// </summary>
    internal static Category Rehydrate(
        Guid id,
        string name,
        Slug slug,
        Guid? parentCategoryId,
        string? description,
        int displayOrder,
        bool active,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int version)
    {
        return new Category(id, name, slug, parentCategoryId, description, createdAt)
        {
            DisplayOrder = displayOrder,
            Active = active,
            UpdatedAt = updatedAt,
            Version = version,
        };
    }
}
