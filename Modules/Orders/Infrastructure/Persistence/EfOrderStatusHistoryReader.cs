using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence;

public sealed class EfOrderStatusHistoryReader : IOrderStatusHistoryReader
{
    private readonly OrdersDbContext _dbContext;

    public EfOrderStatusHistoryReader(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OrderStatusHistoryEntry>> ListAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _dbContext.StatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.Sequence)
            .Select(h => new OrderStatusHistoryEntry(h.FromStatus, h.ToStatus, h.Reason, h.ChangedAt))
            .ToListAsync(cancellationToken);
}
