using OrderCore.Api.Modules.Inventory.Application.Contracts;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

public sealed class ConsumeReservationUseCase
{
    private readonly IStockItemRepository _stockItems;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ConsumeReservationUseCase(
        IStockItemRepository stockItems, IInventoryReservationRepository reservations, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        var reservation = await _reservations.GetByIdAsync(reservationId, cancellationToken)
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' was not found.");

        var stockItem = await _stockItems.GetByProductIdAsync(reservation.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"No stock record for product '{reservation.ProductId}'.");

        reservation.Consume(_timeProvider.GetUtcNow());
        stockItem.Consume(reservation.Quantity);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
