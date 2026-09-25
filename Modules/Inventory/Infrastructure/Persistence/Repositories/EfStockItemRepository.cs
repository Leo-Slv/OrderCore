using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
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

    public async Task<(IReadOnlyList<StockItem> Items, int TotalCount)> ListAsync(
        StockState? state, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.StockItems.AsNoTracking();
        if (state is { } wanted)
        {
            query = query.Where(InState(wanted));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(s => s.UpdatedAt)
            .ThenBy(s => s.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(StockItemMapper.ToDomain).ToList(), totalCount);
    }

    public async Task<IReadOnlyList<Guid>> ListProductIdsInStateAsync(StockState state, CancellationToken cancellationToken) =>
        await _dbContext.StockItems.AsNoTracking().Where(InState(state)).Select(s => s.ProductId).ToListAsync(cancellationToken);

    public Task<int> CountInStateAsync(StockState state, CancellationToken cancellationToken) =>
        _dbContext.StockItems.Where(InState(state)).CountAsync(cancellationToken);

    /// <summary>
    /// <c>StockItemOutput.StateOf</c> translated to SQL over the stored
    /// columns (available is computed, not stored). The repository tests
    /// check the two agree.
    /// </summary>
    private static Expression<Func<StockItemPersistenceModel, bool>> InState(StockState state) => state switch
    {
        StockState.OutOfStock => s => s.QuantityOnHand - s.QuantityReserved <= 0,
        StockState.LowStock => s => s.QuantityOnHand - s.QuantityReserved > 0
            && s.QuantityOnHand - s.QuantityReserved <= s.ReorderLevel,
        StockState.InStock => s => s.QuantityOnHand - s.QuantityReserved > 0
            && s.QuantityOnHand - s.QuantityReserved > s.ReorderLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown stock state."),
    };

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
