using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// The backoffice product list, which is also the stock screen (backoffice
/// decision 5): every product, whatever its status, each with its stock
/// figures. Filtering by stock state asks Inventory which products are in
/// that state first and then pages Catalog's own products among them, so
/// names, SKUs and paging stay Catalog's while the numbers stay Inventory's.
/// </summary>
public sealed class ListAdminProductsUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockLevels _stockLevels;

    public ListAdminProductsUseCase(IProductRepository products, IStockLevels stockLevels)
    {
        _products = products;
        _stockLevels = stockLevels;
    }

    public async Task<PagedResult<AdminProductSummaryOutput>> ExecuteAsync(ListAdminProductsFilter filter, CancellationToken cancellationToken)
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

        IReadOnlyCollection<Guid>? onlyProductIds = filter.Stock is { } state
            ? await _stockLevels.ListProductIdsInStateAsync(state, cancellationToken)
            : null;

        var (products, totalCount) = await _products.ListForAdminAsync(filter, onlyProductIds, cancellationToken);
        var stock = await _stockLevels.GetStockLevelsAsync(products.Select(p => p.Id).ToList(), cancellationToken);

        return new PagedResult<AdminProductSummaryOutput>
        {
            Items = products.Select(p => AdminProductSummaryOutput.From(p, stock.GetValueOrDefault(p.Id))).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalItems = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }
}
