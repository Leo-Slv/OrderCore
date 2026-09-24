using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Orders.Application.UseCases;

/// <summary>
/// Re-prices a client-side cart against the current catalog and stock,
/// so the storefront can show "price changed" or "no longer available"
/// without re-implementing those rules. Read-only: nothing is reserved,
/// and a clean quote is no guarantee that checkout will succeed a moment
/// later. Checkout checks everything again.
/// </summary>
public sealed class QuoteCartUseCase
{
    private readonly IProductCatalog _productCatalog;
    private readonly IInventoryService _inventoryService;

    public QuoteCartUseCase(IProductCatalog productCatalog, IInventoryService inventoryService)
    {
        _productCatalog = productCatalog;
        _inventoryService = inventoryService;
    }

    public async Task<CartQuote> ExecuteAsync(IReadOnlyList<QuoteCartLine> lines, CancellationToken cancellationToken)
    {
        if (lines.Any(l => l.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(lines), "Every cart line must have a quantity greater than zero.");
        }

        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
        {
            throw new ArgumentException("Each product may appear only once in the cart.", nameof(lines));
        }

        var productIds = lines.Select(l => l.ProductId).ToList();
        var products = await _productCatalog.GetManyAsync(productIds, cancellationToken);
        var available = await _inventoryService.GetAvailableQuantitiesAsync(productIds, cancellationToken);

        var currencies = products.Values.Select(p => p.Currency).Distinct().ToList();
        if (currencies.Count > 1)
        {
            throw new DomainRuleViolationException("mixed_currencies", "Products in the cart are priced in different currencies.");
        }

        var quoted = lines.Select(line => QuoteLine(line, products.GetValueOrDefault(line.ProductId), available[line.ProductId])).ToList();

        return new CartQuote(
            currencies.SingleOrDefault(),
            quoted.Where(IsBuyable).Sum(l => l.LineTotal),
            IsValid: quoted.Count > 0 && quoted.All(l => l.Issue is null),
            quoted);
    }

    private static CartQuoteLine QuoteLine(QuoteCartLine line, CatalogProductSnapshot? product, int availableQuantity)
    {
        if (product is null)
        {
            return new CartQuoteLine(line.ProductId, null, null, null, null, line.Quantity, 0m, CartLineIssue.NotFound, null);
        }

        var issue = !product.IsPurchasable ? CartLineIssue.Unavailable
            : availableQuantity < line.Quantity ? CartLineIssue.InsufficientStock
            : line.ExpectedUnitPrice is { } expected && expected != product.CurrentPrice ? CartLineIssue.PriceChanged
            : (CartLineIssue?)null;

        return new CartQuoteLine(
            line.ProductId,
            product.Name,
            product.Slug,
            product.PrimaryImageUrl,
            product.CurrentPrice,
            line.Quantity,
            product.CurrentPrice * line.Quantity,
            issue,
            issue == CartLineIssue.PriceChanged ? line.ExpectedUnitPrice : null);
    }

    private static bool IsBuyable(CartQuoteLine line) => line.Issue is null or CartLineIssue.PriceChanged;
}
