using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Infrastructure.Persistence.Mappers;

/// <summary>
/// Translates between the <see cref="Category"/> aggregate and its
/// persistence model. 03-catalog.md's CategoryMapper has no ApplyChanges
/// (unlike ProductMapper) — added anyway: <c>EfCategoryRepository</c> needs
/// it to persist mutations made through <c>Rename</c>/<c>ChangeDisplayOrder</c>/
/// <c>Activate</c>/<c>Deactivate</c> the same way EfCustomerRepository does.
/// </summary>
public static class CategoryMapper
{
    public static Category ToDomain(CategoryPersistenceModel model) => Category.Rehydrate(
        model.Id,
        model.Name,
        Slug.Create(model.Slug),
        model.ParentCategoryId,
        model.Description,
        model.DisplayOrder,
        model.Active,
        model.CreatedAt,
        model.UpdatedAt,
        model.Version);

    public static CategoryPersistenceModel ToPersistence(Category domain) => new()
    {
        Id = domain.Id,
        Name = domain.Name,
        Slug = domain.Slug.Value,
        ParentCategoryId = domain.ParentCategoryId,
        Description = domain.Description,
        DisplayOrder = domain.DisplayOrder,
        Active = domain.Active,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt,
        Version = domain.Version,
    };

    public static void ApplyChanges(Category domain, CategoryPersistenceModel model)
    {
        model.Name = domain.Name;
        model.Slug = domain.Slug.Value;
        model.Description = domain.Description;
        model.DisplayOrder = domain.DisplayOrder;
        model.Active = domain.Active;
        model.UpdatedAt = domain.UpdatedAt;
    }
}
