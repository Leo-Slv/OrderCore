using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.UseCases;

/// <summary>
/// Implements section 11's central guarantee: with `Stock = 1` and
/// concurrent reservation requests, exactly one succeeds. `TryReserve`
/// decides in-memory whether enough stock is available; the retry loop
/// here handles the case where a concurrent request already changed the
/// StockItem between the read and this unit of work's save (resolved
/// concurrency decision — see Docs/specs/inventory/stock-and-reservations.md).
/// </summary>
public sealed class ReserveStockUseCase
{
    private const int MaxAttempts = 3;

    private readonly IStockItemRepository _stockItems;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ReserveStockUseCase(
        IStockItemRepository stockItems, IInventoryReservationRepository reservations, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _stockItems = stockItems;
        _reservations = reservations;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<ReserveStockResult> ExecuteAsync(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var stockItem = await _stockItems.GetByProductIdAsync(command.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"No stock record for product '{command.ProductId}'.");

            if (!stockItem.TryReserve(command.Quantity))
            {
                return new ReserveStockResult(null, false);
            }

            var now = _timeProvider.GetUtcNow();
            var reservation = InventoryReservation.Create(command.ProductId, command.OrderId, command.OrderItemId, command.Quantity, now);
            await _reservations.AddAsync(reservation, cancellationToken);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new ReserveStockResult(reservation.Id, true);
            }
            catch (StockConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Another request changed this StockItem first; loop
                // around to reload it and try again. On the last attempt
                // the exception is not caught and propagates instead.
            }
        }
    }
}
