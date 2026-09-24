namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

/// <summary>
/// <see cref="ExpectedUnitPrice"/> is the price the cart is currently
/// showing for this product; send it to be told when it changed.
/// </summary>
public sealed class QuoteCartLineRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    public decimal? ExpectedUnitPrice { get; init; }
}
