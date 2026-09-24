namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// <see cref="Total"/> sums the lines that can still be bought, at current
/// prices. <see cref="IsValid"/> is true only when the cart has lines and
/// none of them has an issue, i.e. it can go to checkout as it is.
/// <see cref="Currency"/> is null when no line matched a product.
/// </summary>
public sealed class CartQuoteResponse
{
    public string? Currency { get; init; }

    public decimal Total { get; init; }

    public bool IsValid { get; init; }

    public IReadOnlyList<CartQuoteLineResponse> Lines { get; init; } = [];
}
