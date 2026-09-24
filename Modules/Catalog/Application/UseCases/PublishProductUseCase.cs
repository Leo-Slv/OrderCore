using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Catalog.Application.Contracts;
using OrderCore.Api.Modules.Catalog.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Catalog.Application.UseCases;

public sealed class PublishProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IStockAvailabilityProvider _availability;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public PublishProductUseCase(
        IProductRepository products, IStockAvailabilityProvider availability, IAuditLogService auditLog, TimeProvider timeProvider)
    {
        _products = products;
        _availability = availability;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task<ProductOutput> ExecuteAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken)
            ?? throw new NotFoundException("product_not_found", $"Product '{productId}' was not found.");

        product.Publish(_timeProvider.GetUtcNow());
        await _products.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(AuditLogActionNames.ProductPublished, "Product", productId, metadata: null, userId: null, cancellationToken);

        var availability = await _availability.GetAvailabilityAsync(product.Id, cancellationToken);
        return ProductOutput.From(product, availability);
    }
}
