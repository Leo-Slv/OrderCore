using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Modules.Catalog.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class CreateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly IStockLevels _stockLevels;
    private readonly ICategoryRepository _categories;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public CreateProductUseCase(
        IProductRepository products,
        IStockAvailabilityProvider availability,
        IStockLevels stockLevels,
        ICategoryRepository categories,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _products = products;
        _availability = availability;
        _stockLevels = stockLevels;
        _categories = categories;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<ProductOutput> ExecuteAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(command.CategoryId, cancellationToken)
            ?? throw new NotFoundException("category_not_found", $"Category '{command.CategoryId}' was not found.");

        var existing = await _products.GetBySkuAsync(command.Sku, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("sku_already_exists", $"A product with SKU '{command.Sku}' already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var slug = await GenerateUniqueSlugAsync(command.Name, command.Sku, cancellationToken);
        var product = Product.Create(
            command.Sku, command.Name, slug, category.Id, command.CurrentPrice, command.Currency, now);

        await _products.AddAsync(product, cancellationToken);
        await _products.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.ProductCreated,
            "Product",
            product.Id,
            new Dictionary<string, string?> { ["sku"] = product.Sku },
            userId: null,
            cancellationToken);

        // After the product is saved: if this fails, the product exists without
        // a stock record until it is published, which ensures it again.
        await _stockLevels.EnsureStockRecordAsync(product.Id, cancellationToken);

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }

    /// <summary>
    /// Slugs identify product pages, so they must be unique (enforced by a
    /// unique index). The name alone is tried first; on a collision the
    /// SKU is appended, and SKUs are already unique. A second collision
    /// is only possible if another product's name happens to produce
    /// this exact "name-sku" slug, so it is reported as a conflict
    /// instead of retrying forever.
    /// </summary>
    private async Task<Slug> GenerateUniqueSlugAsync(string name, string sku, CancellationToken cancellationToken)
    {
        var slug = Slug.GenerateFrom(name);
        if (await _products.GetBySlugAsync(slug, cancellationToken) is null)
        {
            return slug;
        }

        var withSku = Slug.GenerateFrom($"{name} {sku}");
        if (await _products.GetBySlugAsync(withSku, cancellationToken) is null)
        {
            return withSku;
        }

        throw new ConflictException("slug_already_exists", $"Could not generate a unique slug for product '{name}'.");
    }
}
