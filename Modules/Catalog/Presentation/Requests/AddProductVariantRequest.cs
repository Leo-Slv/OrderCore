namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

public sealed class AddProductVariantRequest
{
    /// <summary>Unique among the product's variants.</summary>
    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>What sets this variant apart, e.g. <c>{"size": "M", "color": "blue"}</c>.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();

    /// <summary>Added to the product's price for this variant; may be 0.</summary>
    public decimal AdditionalPrice { get; init; }
}
