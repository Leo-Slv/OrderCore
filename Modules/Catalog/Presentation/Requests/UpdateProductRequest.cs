namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

/// <summary>
/// <see cref="Description"/> is not in 03-catalog.md's request shape, but
/// UpdateProductCommand requires one (it always overwrites the current
/// value) — without it, this endpoint could never set or clear it.
/// </summary>
public sealed class UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public string? ShortDescription { get; init; }

    public string? Description { get; init; }

    public string? Brand { get; init; }
}
