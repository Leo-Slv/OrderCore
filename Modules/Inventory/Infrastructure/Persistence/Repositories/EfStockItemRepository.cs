using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;

/// <summary>
/// No `SaveChangesAsync` of its own — see <c>IUnitOfWork</c>'s remarks.
/// <see cref="GetByProductIdAsync"/> always drops any previously tracked
/// entry for the product before querying, so <c>ReserveStockUseCase</c>'s
/// retry loop actually sees fresh data on each attempt instead of the
/// same stale in-memory instance EF Core's identity map would otherwise
/// hand back.
/// </summary>
public sealed class EfStockItemRepository : IStockItemRepository, IPendingChangesTracker
{
    private readonly InventoryDbContext _dbContext;
    private readonly Dictionary<Guid, (StockItem Domain, StockItemPersistenceModel Model)> _tracked = new();

    public EfStockItemRepository(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (_tracked.TryGetValue(productId, out var existing))
        {
            _dbContext.Entry(existing.Model).State = EntityState.Detached;
            _tracked.Remove(productId);
        }

        var model = await _dbContext.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);
        if (model is null)
        {
            return null;
        }

        var domain = StockItemMapper.ToDomain(model);
        _tracked[productId] = (domain, model);
        return domain;
    }

    /// <summary>
    /// <c>AsNoTracking</c> and never added to <c>_tracked</c>: this is a read
    /// path, and it must not change which instance a later
    /// <see cref="GetByProductIdAsync"/> in the same unit of work gets back.
    /// </summary>
    public async Task<IReadOnlyList<StockItem>> ListByProductIdsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var models = await _dbContext.StockItems
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ProductId))
            .ToListAsync(cancellationToken);

        return models.Select(StockItemMapper.ToDomain).ToList();
    }

    public async Task AddAsync(StockItem stockItem, CancellationToken cancellationToken)
    {
        var model = StockItemMapper.ToPersistence(stockItem);
        await _dbContext.StockItems.AddAsync(model, cancellationToken);
        _tracked[stockItem.ProductId] = (stockItem, model);
    }

    void IPendingChangesTracker.ApplyPendingChanges()
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            StockItemMapper.ApplyChanges(domain, model);
        }
    }

    IReadOnlyCollection<IDomainEvent> IPendingChangesTracker.CollectAndClearDomainEvents()
    {
        var events = _tracked.Values.SelectMany(t => t.Domain.DomainEvents).ToList();

        foreach (var (domain, _) in _tracked.Values)
        {
            domain.ClearDomainEvents();
        }

        return events;
    }

    void IPendingChangesTracker.ForgetTrackedEntries()
    {
        foreach (var (_, model) in _tracked.Values)
        {
            _dbContext.Entry(model).State = EntityState.Detached;
        }

        _tracked.Clear();
    }
}
