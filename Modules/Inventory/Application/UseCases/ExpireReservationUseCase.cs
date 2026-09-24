using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Takes <see cref="IStockItemRepository"/> too — 04-inventory.md wires
/// this use case to <see cref="IInventoryReservationRepository"/> only,
/// which would leave an expired reservation's quantity permanently stuck
/// as "reserved" on the StockItem (resolved deviation — see
/// Docs/specs/inventory/stock-and-reservations.md).
/// </summary>
public sealed class ExpireReservationUseCase
{
    private readonly IInventoryReservationRepository _reservations;
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLog;

    public ExpireReservationUseCase(
        IInventoryReservationRepository reservations, IStockItemRepository stockItems, IUnitOfWork unitOfWork, IAuditLogService auditLog)
    {
        _reservations = reservations;
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
    }

    public async Task ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new NotFoundException("reservation_not_found", $"Reservation '{reservationId}' was not found.");

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{reservation.ProductId}'.");

        reservation.Expire();
        stockItem.Release(reservation.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditLog.RecordAsync(
            AuditLogActionNames.InventoryExpired, "InventoryReservation", reservationId, metadata: null, userId: null, cancellationToken);
    }
}
