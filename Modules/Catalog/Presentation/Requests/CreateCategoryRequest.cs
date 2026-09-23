namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

/// <summary>
/// <see cref="Description"/> is not in 03-catalog.md's request shape, but
/// CreateCategoryCommand takes one — same gap already documented there.
/// </summary>
public sealed class CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public Guid? ParentCategoryId { get; init; }

    public string? Description { get; init; }
}
