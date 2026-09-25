using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;

namespace OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;

public sealed class EfStockMovementReader : IStockMovementReader
{
    private readonly InventoryDbContext _dbContext;

    public EfStockMovementReader(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<StockMovementOutput> Items, int TotalCount)> ListByProductIdAsync(
        Guid productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.StockMovements.AsNoTracking().Where(m => m.ProductId == productId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new StockMovementOutput(
                m.Id, m.ProductId, m.MovementType, m.Quantity, m.ReferenceType, m.ReferenceId, m.Reason, m.CreatedAt))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
