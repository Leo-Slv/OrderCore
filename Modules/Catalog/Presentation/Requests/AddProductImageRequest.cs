namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

public sealed class AddProductImageRequest
{
    /// <summary>Absolute http(s) address of the image.</summary>
    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }

    /// <summary>Makes this the product's primary image, replacing the current one.</summary>
    public bool IsPrimary { get; init; }
}
