using System.Globalization;
using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// Starts (or ends, with <c>null</c>) a promotion: the "was" price the
/// storefront shows struck through, and what its <c>onSale</c> filter
/// looks for.
/// </summary>
public sealed class SetCompareAtPriceUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly IAuditLogService _auditLog;

    public SetCompareAtPriceUseCase(IProductRepository products, IStockAvailabilityProvider availability, IAuditLogService auditLog)
    {
        _products = products;
        _availability = availability;
        _auditLog = auditLog;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, decimal? compareAtPrice, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        product.SetCompareAtPrice(compareAtPrice);
        await _products.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.ProductPromotionChanged,
            "Product",
            productId,
            new Dictionary<string, string?> { ["compareAtPrice"] = compareAtPrice?.ToString(CultureInfo.InvariantCulture) },
            userId: null,
            cancellationToken);

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
