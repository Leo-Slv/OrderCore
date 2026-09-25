namespace OrderCore.Api.Modules.Catalog.Presentation.Requests;

public sealed class SetCompareAtPriceRequest
{
    /// <summary>The "was" price shown struck through; must be above the current price. Null ends the promotion.</summary>
    public decimal? CompareAtPrice { get; init; }
}
