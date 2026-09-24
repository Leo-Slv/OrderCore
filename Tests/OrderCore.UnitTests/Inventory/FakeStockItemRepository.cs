using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.UnitTests.Inventory;

internal sealed class FakeStockItemRepository : IStockItemRepository
{
    private readonly Dictionary<Guid, StockItem> _stockItems = new();

    public Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_stockItems.GetValueOrDefault(productId));

    public Task<IReadOnlyList<StockItem>> ListByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItem>>(_stockItems.Values.Where(s => productIds.Contains(s.ProductId)).ToList());

    public Task AddAsync(StockItem stockItem, CancellationToken cancellationToken)
    {
        _stockItems[stockItem.ProductId] = stockItem;
        return Task.CompletedTask;
    }
}
