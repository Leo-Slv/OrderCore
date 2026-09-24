namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <see cref="Issue"/> is null for a line that is fine, otherwise the most
/// serious of <c>NotFound</c>, <c>Unavailable</c>, <c>InsufficientStock</c>,
/// <c>PriceChanged</c>. Product fields are null only for <c>NotFound</c>.
/// <see cref="PreviousUnitPrice"/> is the price the client sent, set only
/// for <c>PriceChanged</c>.
/// </summary>
public sealed class CartQuoteLineResponse
{
    public Guid ProductId { get; init; }

    public string? ProductName { get; init; }

    public string? Slug { get; init; }

    public string? ImageUrl { get; init; }

    public decimal? UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }

    public string? Issue { get; init; }

    public decimal? PreviousUnitPrice { get; init; }
}
