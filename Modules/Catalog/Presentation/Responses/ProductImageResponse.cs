namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

public sealed class ProductImageResponse
{
    public Guid Id { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }

    public bool IsPrimary { get; init; }

    public int DisplayOrder { get; init; }
}
