using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class GetProductByIdUseCase
{
    private readonly IProductRepository _products;

    public GetProductByIdUseCase(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

        return ProductOutput.From(product);
    }
}
