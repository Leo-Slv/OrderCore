using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Coordinates <see cref="EfStockItemRepository"/> and
/// <see cref="EfInventoryReservationRepository"/> into a single atomic
/// save over their shared <see cref="InventoryDbContext"/> — see
/// <c>IUnitOfWork</c>'s remarks (Application layer) for why. This is also
/// the first place in the codebase that actually calls
/// <see cref="IDomainEventDispatcher.DispatchAsync"/>: it happens right
/// after the save succeeds, over every tracked aggregate's domain events
/// from both repositories combined.
/// </summary>
public sealed class InventoryUnitOfWork : IUnitOfWork
{
    private readonly InventoryDbContext _dbContext;
    private readonly EfStockItemRepository _stockItemRepository;
    private readonly EfInventoryReservationRepository _reservationRepository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public InventoryUnitOfWork(
        InventoryDbContext dbContext,
        EfStockItemRepository stockItemRepository,
        EfInventoryReservationRepository reservationRepository,
        IDomainEventDispatcher domainEventDispatcher)
    {
        _dbContext = dbContext;
        _stockItemRepository = stockItemRepository;
        _reservationRepository = reservationRepository;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var stockItemTracker = (IPendingChangesTracker)_stockItemRepository;
        var reservationTracker = (IPendingChangesTracker)_reservationRepository;

        stockItemTracker.ApplyPendingChanges();
        reservationTracker.ApplyPendingChanges();

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            stockItemTracker.ForgetTrackedEntries();
            reservationTracker.ForgetTrackedEntries();
            throw new StockConcurrencyConflictException("A concurrent update changed the stock item.", ex);
        }

        var events = stockItemTracker.CollectAndClearDomainEvents()
            .Concat(reservationTracker.CollectAndClearDomainEvents())
            .ToList();

        if (events.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        }
    }
}
