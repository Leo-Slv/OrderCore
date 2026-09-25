using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// A product's variants (size, color…). Stock stays per product, not per
/// variant (out of scope). Each returns the updated product.
/// </summary>
public sealed class ManageProductVariantsUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly TimeProvider _timeProvider;

    public ManageProductVariantsUseCase(IProductRepository products, IStockAvailabilityProvider availability, TimeProvider timeProvider)
    {
        _products = products;
        _availability = availability;
        _timeProvider = timeProvider;
    }

    public Task<ProductOutput> AddAsync(
        Guid productId, string sku, string name, string attributesJson, decimal additionalPrice, CancellationToken cancellationToken) =>
        ChangeAsync(
            productId,
            product => product.AddVariant(sku, name, attributesJson, additionalPrice, _timeProvider.GetUtcNow()),
            cancellationToken);

    public Task<ProductOutput> RemoveAsync(Guid productId, Guid variantId, CancellationToken cancellationToken) =>
        ChangeAsync(productId, product => product.RemoveVariant(variantId), cancellationToken);

    private async Task<ProductOutput> ChangeAsync(Guid productId, Action<Product> change, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        change(product);
        await _products.SaveChangesAsync(cancellationToken);

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
