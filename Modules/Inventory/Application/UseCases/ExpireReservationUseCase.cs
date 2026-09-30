using OrderCore.Api.Modules.AuditLogs.Application.Constants;
using OrderCore.Api.Modules.AuditLogs.Application.Services;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.Telemetry;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Takes <see cref="IStockItemRepository"/> too — 04-inventory.md wires
/// this use case to <see cref="IInventoryReservationRepository"/> only,
/// which would leave an expired reservation's quantity permanently stuck
/// as "reserved" on the StockItem (resolved deviation — see
/// Docs/specs/inventory/stock-and-reservations.md).
/// </summary>
/// <summary>What <see cref="ExpireReservationUseCase"/> expired.</summary>
public sealed record ExpiredReservation(Guid ReservationId, Guid OrderId, Guid ProductId, int Quantity);

public sealed class ExpireReservationUseCase
{
    private readonly IInventoryReservationRepository _reservations;
    private readonly IStockItemRepository _stockItems;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLog;
    private readonly InventoryMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public ExpireReservationUseCase(
        IInventoryReservationRepository reservations,
        IStockItemRepository stockItems,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLog,
        InventoryMetrics metrics,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _reservations = reservations;
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
        _auditLog = auditLog;
        _metrics = metrics;
    }

    /// <returns>
    /// What was expired, for the caller to report; null when the reservation
    /// doesn't exist or has moved on (released or consumed meanwhile) — nothing
    /// to do then, rather than an error the expiry job would keep retrying.
    /// </returns>
    public async Task<ExpiredReservation?> ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken);
        if (reservation is not { Status: ReservationStatus.Reserved })
        {
            return null;
        }

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{reservation.ProductId}'.");

        reservation.Expire();
        stockItem.Release(reservation.Quantity, _timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _metrics.Released("expired");

        await _auditLog.RecordAsync(
            AuditLogActionNames.InventoryExpired, "InventoryReservation", reservationId, metadata: null, userId: null, cancellationToken);

        return new ExpiredReservation(reservation.Id, reservation.OrderId, reservation.ProductId, reservation.Quantity);
    }
}
