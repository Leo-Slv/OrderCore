using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// A product's gallery: add (appended last; optionally the new primary),
/// remove, and put in a new order. Each returns the updated product.
/// </summary>
public sealed class ManageProductImagesUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly TimeProvider _timeProvider;

    public ManageProductImagesUseCase(IProductRepository products, IStockAvailabilityProvider availability, TimeProvider timeProvider)
    {
        _products = products;
        _availability = availability;
        _timeProvider = timeProvider;
    }

    public Task<ProductOutput> AddAsync(Guid productId, string url, string? altText, bool isPrimary, CancellationToken cancellationToken) =>
        ChangeAsync(productId, product => product.AddImage(url, altText, isPrimary, _timeProvider.GetUtcNow()), cancellationToken);

    public Task<ProductOutput> RemoveAsync(Guid productId, Guid imageId, CancellationToken cancellationToken) =>
        ChangeAsync(productId, product => product.RemoveImage(imageId), cancellationToken);

    public Task<ProductOutput> ReorderAsync(Guid productId, IReadOnlyList<Guid> orderedImageIds, CancellationToken cancellationToken) =>
        ChangeAsync(productId, product => product.ReorderImages(orderedImageIds), cancellationToken);

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
