using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence;

public sealed class EfOrderTimelineReader : IOrderTimelineReader
{
    private readonly OrdersDbContext _dbContext;

    public EfOrderTimelineReader(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OrderTimelineEntry>> ListAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Timeline
            .AsNoTracking()
            .Where(e => e.OrderId == orderId)
            .OrderBy(e => e.OccurredAt)
            .ThenBy(e => e.Sequence)
            .ToListAsync(cancellationToken);

        return rows
            .Select(e => new OrderTimelineEntry(
                e.EventId,
                e.Type,
                e.Source,
                e.OccurredAt,
                JsonSerializer.Deserialize<Dictionary<string, string?>>(e.DetailsJson) ?? []))
            .ToList();
    }
}
