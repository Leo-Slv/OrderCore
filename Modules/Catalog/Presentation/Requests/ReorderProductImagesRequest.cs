namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

public sealed class ReorderProductImagesRequest
{
    /// <summary>Every image id of the product, exactly once, in the new display order.</summary>
    public IReadOnlyList<Guid> ImageIds { get; init; } = [];
}
