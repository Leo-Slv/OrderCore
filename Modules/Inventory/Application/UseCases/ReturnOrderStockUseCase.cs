using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Puts back on hand what a cancelled order had already consumed
/// (backoffice decision 2). Reservations still held are released by
/// <c>ReleaseReservationUseCase</c> instead; this only touches
/// <see cref="ReservationStatus.Consumed"/> ones, so it is idempotent:
/// repeating it finds them <see cref="ReservationStatus.Returned"/> and
/// does nothing. Every reservation and stock item of the order is saved in
/// one unit of work, retried on a concurrency conflict the same way
/// <c>ReserveStockUseCase</c> is.
/// </summary>
public sealed class ReturnOrderStockUseCase
{
    private const int MaxAttempts = 3;

    private readonly IStockItemRepository _stockItems;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReturnOrderStockUseCase(
        IStockItemRepository stockItems,
        IInventoryReservationRepository reservations,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    /// <returns>How many units went back on hand.</returns>
    public async Task<int> ExecuteAsync(Guid orderId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var consumed = (await _reservations.ListByOrderIdAsync(orderId, cancellationToken))
                .Where(r => r.Status == ReservationStatus.Consumed)
                .ToList();

            if (consumed.Count == 0)
            {
                return 0;
            }

            var now = _timeProvider.GetUtcNow();

            // One load per product: loading the same product twice would
            // detach the first, already-changed instance.
            foreach (var byProduct in consumed.GroupBy(r => r.ProductId))
            {
                var stockItem = await _stockItems.GetByProductIdAsync(byProduct.Key, cancellationToken)
                    ?? throw new NotFoundException("stock_item_not_found", $"No stock record for product '{byProduct.Key}'.");

                foreach (var reservation in byProduct)
                {
                    reservation.Return(now);
                    stockItem.ReturnConsumed(reservation.Quantity, now);
                }
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return consumed.Sum(r => r.Quantity);
            }
            catch (StockConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // A reservation or sale changed one of these stock items
                // first; reload everything and try again.
            }
        }
    }
}
