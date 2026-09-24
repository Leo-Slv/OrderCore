using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class GetProductByIdUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;

    public GetProductByIdUseCase(IProductRepository products, IStockAvailabilityProvider availability)
    {
        _products = products;
        _availability = availability;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
