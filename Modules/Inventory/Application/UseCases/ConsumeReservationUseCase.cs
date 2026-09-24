using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

public sealed class ConsumeReservationUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public ConsumeReservationUseCase(
        IStockItemRepository stockItems,
        IInventoryReservationRepository reservations,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLog,
        TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new NotFoundException("reservation_not_found", $"Reservation '{reservationId}' was not found.");

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{reservation.ProductId}'.");

        reservation.Consume(_timeProvider.GetUtcNow());
        stockItem.Consume(reservation.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.InventoryConsumed, "InventoryReservation", reservationId, metadata: null, userId: null, cancellationToken);
    }
}
