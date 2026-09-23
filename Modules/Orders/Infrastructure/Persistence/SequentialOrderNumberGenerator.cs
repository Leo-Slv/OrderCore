using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence;

/// <summary>
/// Backs <see cref="IOrderNumberGenerator"/> with a PostgreSQL sequence
/// (<c>order_number_seq</c>, created by the <c>InitialOrdersSchema</c>
/// migration) rather than e.g. counting existing rows — a sequence is the
/// only way to hand out a distinct number under concurrent order creation
/// without a table lock. Formats as <c>ORD-{year}-{6-digit sequence}</c>,
/// per 05-orders.md's own example (<c>"ORD-2024-000123"</c>).
/// </summary>
public sealed class SequentialOrderNumberGenerator : IOrderNumberGenerator
{
    private readonly OrdersDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SequentialOrderNumberGenerator(OrdersDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        var next = await _dbContext.Database
            .SqlQueryRaw<long>("SELECT nextval('order_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return $"ORD-{_timeProvider.GetUtcNow().Year}-{next:D6}";
    }
}
