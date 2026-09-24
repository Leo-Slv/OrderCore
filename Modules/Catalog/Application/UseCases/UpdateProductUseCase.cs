using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class UpdateProductUseCase
{
    private readonly IProductRepository _products;

    public UpdateProductUseCase(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        product.UpdateDetails(command.Name, command.ShortDescription, command.Description, command.Brand);
        await _products.SaveChangesAsync(cancellationToken);

        return ProductOutput.From(product);
    }
}
