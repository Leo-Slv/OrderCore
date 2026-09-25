using OrderCore.Api.Modules.Inventory.Domain.Events;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.EventHandlers;

/// <summary>
/// Writes an audit-trail row for every <see cref="InventoryStockMovementRecorded"/>
/// event — the first real <see cref="IDomainEventHandler{TEvent}"/> in the
/// codebase (resolved decision — see
/// Docs/specs/inventory/stock-and-reservations.md). Calls its own
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> rather than
/// relying on the unit of work's save: dispatch happens from
/// <c>InventoryUnitOfWork.SaveChangesAsync</c> *after* its own save has
/// already completed (so a handler failure can never roll back the
/// aggregate change that produced the event), which means this row needs
/// its own, separate commit — a small, accepted trade-off (the audit row
/// lands in a second transaction) for never risking the stock/reservation
/// write itself.
/// </summary>
public sealed class StockMovementRecorder : IDomainEventHandler<InventoryStockMovementRecorded>
{
    private readonly InventoryDbContext _dbContext;

    public StockMovementRecorder(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task HandleAsync(InventoryStockMovementRecorded domainEvent, CancellationToken cancellationToken)
    {
        var movement = new StockMovementPersistenceModel
        {
            Id = Guid.NewGuid(),
            ProductId = domainEvent.ProductId,
            MovementType = domainEvent.MovementType.ToString(),
            Quantity = domainEvent.Quantity,
            ReferenceType = domainEvent.ReferenceType,
            ReferenceId = domainEvent.ReferenceId,
            Reason = domainEvent.Reason,
            CreatedAt = domainEvent.OccurredAt,
        };

        await _dbContext.StockMovements.AddAsync(movement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
