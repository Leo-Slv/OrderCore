namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>
/// <see cref="ExpectedUnitPrice"/> is the price the client last showed
/// for this line. When sent and different from the current price, the
/// line is reported as <see cref="CartLineIssue.PriceChanged"/>.
/// </summary>
public sealed record QuoteCartLine(Guid ProductId, int Quantity, decimal? ExpectedUnitPrice);

/// <summary>
/// What is wrong with a cart line, most serious first. Only the most
/// serious one is reported per line.
/// </summary>
public enum CartLineIssue
{
    NotFound,
    Unavailable,
    InsufficientStock,
    PriceChanged,
}

/// <summary>
/// Product fields are null only for a <see cref="CartLineIssue.NotFound"/>
/// line. <see cref="PreviousUnitPrice"/> is set only for
/// <see cref="CartLineIssue.PriceChanged"/>.
/// </summary>
public sealed record CartQuoteLine(
    Guid ProductId,
    string? ProductName,
    string? Slug,
    string? ImageUrl,
    decimal? UnitPrice,
    int Quantity,
    decimal LineTotal,
    CartLineIssue? Issue,
    decimal? PreviousUnitPrice);

/// <summary>
/// <see cref="Total"/> sums the lines that can be bought (no issue, or only
/// a price change, at the new price). <see cref="IsValid"/> means the cart
/// can go to checkout as it is: it has lines and none of them has an issue.
/// </summary>
public sealed record CartQuote(string? Currency, decimal Total, bool IsValid, IReadOnlyList<CartQuoteLine> Lines);
