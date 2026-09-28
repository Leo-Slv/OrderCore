using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Infrastructure.Messaging;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Infrastructure.Messaging;

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
/// <para>
/// Those events are collected before the save, so their integration
/// counterparts (<see cref="InventoryIntegrationEventTranslator"/>) go into
/// the Inventory outbox in the same transaction. A failed save loses them
/// along with the change itself: the caller reloads and retries.
/// </para>
/// </summary>
public sealed class InventoryUnitOfWork : IUnitOfWork
{
    /// <summary>The unique index on <c>stock_items.ProductId</c> (one stock record per product).</summary>
    private const string StockItemProductIndex = "IX_stock_items_ProductId";

    private readonly InventoryDbContext _dbContext;
    private readonly EfStockItemRepository _stockItemRepository;
    private readonly EfInventoryReservationRepository _reservationRepository;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly IInventoryOutbox _outbox;

    public InventoryUnitOfWork(
        InventoryDbContext dbContext,
        EfStockItemRepository stockItemRepository,
        EfInventoryReservationRepository reservationRepository,
        IDomainEventDispatcher domainEventDispatcher,
        IInventoryOutbox outbox)
    {
        _outbox = outbox;
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

        var events = stockItemTracker.CollectAndClearDomainEvents()
            .Concat(reservationTracker.CollectAndClearDomainEvents())
            .ToList();
        foreach (var domainEvent in events)
        {
            if (InventoryIntegrationEventTranslator.Translate(domainEvent) is { } integrationEvent)
            {
                _outbox.Enqueue(integrationEvent);
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            stockItemTracker.ForgetTrackedEntries();
            reservationTracker.ForgetTrackedEntries();
            DiscardUnsavedOutboxMessages();
            throw new StockConcurrencyConflictException("A concurrent update changed the stock item.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: StockItemProductIndex })
        {
            stockItemTracker.ForgetTrackedEntries();
            reservationTracker.ForgetTrackedEntries();
            DiscardUnsavedOutboxMessages();
            throw new DuplicateStockItemException("The product already has a stock record.", ex);
        }

        if (events.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        }
    }

    /// <summary>
    /// The events of a save that failed describe changes that never
    /// happened; a retry in the same scope (<c>ReserveStockUseCase</c>)
    /// must not write them along with its own.
    /// </summary>
    private void DiscardUnsavedOutboxMessages()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries<OutboxMessage>().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
