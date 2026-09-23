using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class ChangeProductPriceUseCase
{
    private readonly IProductRepository _products;
    private readonly TimeProvider _timeProvider;

    public ChangeProductPriceUseCase(IProductRepository products, TimeProvider timeProvider)
    {
        _products = products;
        _timeProvider = timeProvider;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, decimal newPrice, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

        product.ChangePrice(newPrice, _timeProvider.GetUtcNow());
        await _products.SaveChangesAsync(cancellationToken);

        return ProductOutput.From(product);
    }
}
