using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class PublishProductUseCase
{
    private readonly IProductRepository _products;
    private readonly TimeProvider _timeProvider;

    public PublishProductUseCase(IProductRepository products, TimeProvider timeProvider)
    {
        _products = products;
        _timeProvider = timeProvider;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

        product.Publish(_timeProvider.GetUtcNow());
        await _products.SaveChangesAsync(cancellationToken);

        return ProductOutput.From(product);
    }
}
