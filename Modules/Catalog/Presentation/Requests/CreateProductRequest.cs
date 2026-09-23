namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

/// <summary>
/// <see cref="Currency"/> is not in 03-catalog.md's request shape, but
/// CreateProductCommand requires one — same gap already documented there.
/// </summary>
public sealed class CreateProductRequest
{
    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public decimal CurrentPrice { get; init; }

    public string Currency { get; init; } = string.Empty;
}
