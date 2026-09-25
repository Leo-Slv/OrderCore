using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

/// <summary>
/// Takes a product off sale for good: it leaves the storefront (listing,
/// product page, cart and checkout all treat it as unavailable) but stays
/// in the backoffice and in the orders that already bought it.
/// </summary>
public sealed class DiscontinueProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly IAuditLogService _auditLog;

    public DiscontinueProductUseCase(IProductRepository products, IStockAvailabilityProvider availability, IAuditLogService auditLog)
    {
        _products = products;
        _availability = availability;
        _auditLog = auditLog;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        product.Discontinue();
        await _products.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(AuditLogActionNames.ProductDiscontinued, "Product", productId, metadata: null, userId: null, cancellationToken);

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
