using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.Api.Modules.Inventory.Application.Contracts;

/// <summary>
/// Persistence abstraction the Application layer depends on. No
/// `SaveChangesAsync` (unlike every other repository in the project) —
/// see <see cref="IUnitOfWork"/>'s remarks on why Inventory needs one
/// shared commit point instead.
/// </summary>
public interface IStockItemRepository
{
    Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only snapshot for availability queries: the returned items are
    /// not tracked by the unit of work, so changing them saves nothing. Use
    /// <see cref="GetByProductIdAsync"/> to load a stock item for a change.
    /// </summary>
    Task<IReadOnlyList<StockItem>> ListByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task AddAsync(StockItem stockItem, CancellationToken cancellationToken);
}
