using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class ListProductsUseCase
{
    private readonly IProductRepository _products;

    public ListProductsUseCase(IProductRepository products)
    {
        _products = products;
    }

    public async Task<IReadOnlyList<ProductOutput>> ExecuteAsync(ListProductsFilter filter, CancellationToken cancellationToken)
    {
        var products = await _products.ListAsync(filter, cancellationToken);

        return products.Select(ProductOutput.From).ToList();
    }
}
