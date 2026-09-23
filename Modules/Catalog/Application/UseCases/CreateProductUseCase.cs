using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class CreateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly TimeProvider _timeProvider;

    public CreateProductUseCase(IProductRepository products, ICategoryRepository categories, TimeProvider timeProvider)
    {
        _products = products;
        _categories = categories;
        _timeProvider = timeProvider;
    }

    public async Task<ProductOutput> ExecuteAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
            ?? throw new InvalidOperationException($"Category '{command.CategoryId}' was not found.");

        var existing = await _products.GetBySkuAsync(command.Sku, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A product with SKU '{command.Sku}' already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var product = Product.Create(
            command.Sku, command.Name, Slug.GenerateFrom(command.Name), category.Id, command.CurrentPrice, command.Currency, now);

        await _products.AddAsync(product, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        return ProductOutput.From(product);
    }
}
