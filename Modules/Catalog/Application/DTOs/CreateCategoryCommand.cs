namespace OrderCore.Api.Modules.Catalog.Application.DTOs;

/// <summary>
/// <see cref="Description"/> is not in 03-catalog.md's command shape, but
/// <c>Category.Create</c> takes one — added the same way it was added to
/// <c>Category.Create</c> itself. There is no Slug field: the use case
/// derives one from <see cref="Name"/> via <c>Slug.GenerateFrom</c>.
/// </summary>
public sealed record CreateCategoryCommand(string Name, Guid? ParentCategoryId, string? Description);
