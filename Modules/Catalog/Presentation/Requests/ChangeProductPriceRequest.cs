namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

public sealed class ChangeProductPriceRequest
{
    /// <summary>
    /// The new price. A compare-at price that isn't above it anymore is
    /// cleared (the promotion ends).
    /// </summary>
    public decimal NewPrice { get; init; }
}
