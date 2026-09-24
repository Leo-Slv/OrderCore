using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Inventory.Application.Contracts;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

public sealed class ReleaseReservationUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;

    public ReleaseReservationUseCase(
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
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' was not found.");

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"No stock record for product '{reservation.ProductId}'.");

        reservation.Release(_timeProvider.GetUtcNow());
        stockItem.Release(reservation.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.InventoryReleased, "InventoryReservation", reservationId, metadata: null, userId: null, cancellationToken);
    }
}
