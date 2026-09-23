using OrderCore.Api.Modules.Catalog.Domain.Entities;

namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

public sealed record CategoryOutput(Guid Id, string Name, string Slug)
{
    public static CategoryOutput From(Category category) => new(category.Id, category.Name, category.Slug.Value);
}
