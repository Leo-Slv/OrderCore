using OrderCore.Api.Modules.Inventory.Application.Contracts;
using OrderCore.Api.Modules.Inventory.Application.DTOs;
using OrderCore.Api.Modules.Inventory.Domain.Entities;

namespace OrderCore.UnitTests.Inventory;

internal sealed class FakeStockItemRepository : IStockItemRepository
{
    private readonly Dictionary<Guid, StockItem> _stockItems = new();

    public Task<StockItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken) =>
        Task.FromResult(_stockItems.GetValueOrDefault(productId));

    public Task<IReadOnlyList<StockItem>> ListByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockItem>>(_stockItems.Values.Where(s => productIds.Contains(s.ProductId)).ToList());

    public Task<(IReadOnlyList<StockItem> Items, int TotalCount)> ListAsync(
        StockState? state, int page, int pageSize, CancellationToken cancellationToken)
    {
        var matching = _stockItems.Values.Where(s => state is null || StockItemOutput.StateOf(s) == state).ToList();
        return Task.FromResult<(IReadOnlyList<StockItem>, int)>((matching.Skip((page - 1) * pageSize).Take(pageSize).ToList(), matching.Count));
    }

    public Task<IReadOnlyList<Guid>> ListProductIdsInStateAsync(StockState state, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(_stockItems.Values.Where(s => StockItemOutput.StateOf(s) == state).Select(s => s.ProductId).ToList());

    public Task<int> CountInStateAsync(StockState state, CancellationToken cancellationToken) =>
        Task.FromResult(_stockItems.Values.Count(s => StockItemOutput.StateOf(s) == state));

    public Task AddAsync(StockItem stockItem, CancellationToken cancellationToken)
    {
        _stockItems[stockItem.ProductId] = stockItem;
        return Task.CompletedTask;
    }
}
