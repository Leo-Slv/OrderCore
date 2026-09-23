namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

public sealed class CategoryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;
}
