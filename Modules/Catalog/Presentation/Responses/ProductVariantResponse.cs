namespace OrderCore.Api.Modules.Catalog.Presentation.Responses;

/// <summary>
/// <see cref="Attributes"/> is the variant's stored JSON object turned
/// into a flat name/value map (e.g. <c>{"color": "black", "size": "M"}</c>),
/// so clients don't have to parse a JSON string.
/// </summary>
public sealed class ProductVariantResponse
{
    public Guid Id { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

    public decimal AdditionalPrice { get; init; }
}
