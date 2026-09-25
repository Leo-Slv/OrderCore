using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// Paged product listing. Availability for the whole page is fetched in
/// one call, not one call per product. Page bounds are validated the same
/// way <c>ListAuditLogsUseCase</c> validates them.
/// </summary>
public sealed class ListProductsUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;

    public ListProductsUseCase(IProductRepository products, IStockAvailabilityProvider availability)
    {
        _products = products;
        _availability = availability;
    }

    /// <param name="includeUnpublished">
    /// Only for admins: drafts, discontinued and deactivated products are
    /// listed too. Everyone else sees published, active products only.
    /// </param>
    public async Task<PagedResult<ProductSummaryOutput>> ExecuteAsync(
        ListProductsFilter filter, bool includeUnpublished, CancellationToken cancellationToken)
    {
        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter), "Page must be greater than or equal to 1.");
        }

        if (filter.PageSize is < 1 or > ListProductsFilter.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                $"PageSize must be between 1 and {ListProductsFilter.MaximumPageSize}.");
        }

        var (products, totalCount) = await _products.ListAsync(filter, publishedOnly: !includeUnpublished, cancellationToken);
        var availability = await _availability.GetAvailabilityAsync(products.Select(p => p.Id).ToList(), cancellationToken);

        return new PagedResult<ProductSummaryOutput>
        {
            Items = products.Select(p => ProductSummaryOutput.From(p, availability[p.Id])).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }
}
