using OrderCore.Api.Modules.Inventory.Application.Contracts;

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

    public ExpireReservationUseCase(IInventoryReservationRepository reservations, IStockItemRepository stockItems, IUnitOfWork unitOfWork)
    {
        _reservations = reservations;
        _stockItems = stockItems;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' was not found.");

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"No stock record for product '{reservation.ProductId}'.");

        reservation.Expire();
        stockItem.Release(reservation.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
