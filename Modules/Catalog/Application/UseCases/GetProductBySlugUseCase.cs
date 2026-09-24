using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// The storefront's product page (<c>/products/:slug</c>). Only a
/// published, active product is returned. A draft, discontinued,
/// deactivated or badly formatted slug gets the same "not found" as a
/// slug that doesn't exist, so the storefront can't tell them apart.
/// </summary>
public sealed class GetProductBySlugUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;

    public GetProductBySlugUseCase(IProductRepository products, IStockAvailabilityProvider availability)
    {
        _products = products;
        _availability = availability;
    }

    public async Task<ProductOutput> ExecuteAsync(string slug, CancellationToken cancellationToken)
    {
        var product = Slug.IsValid(slug)
            ? await _products.GetBySlugAsync(Slug.Create(slug), cancellationToken)
            : null;

        if (product is null || product.Status != ProductStatus.Active || !product.Active)
        {
            throw new NotFoundException("product_not_found", $"Product '{slug}' was not found.");
        }

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
