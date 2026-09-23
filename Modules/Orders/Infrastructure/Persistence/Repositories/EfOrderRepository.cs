using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;

/// <summary>
/// See EfCustomerRepository's remarks: reconciles the domain aggregate
/// against its tracked persistence model right before
/// <see cref="SaveChangesAsync"/>, since IOrderRepository has no explicit
/// UpdateAsync either.
/// </summary>
public sealed class EfOrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _dbContext;
    private readonly Dictionary<Guid, (Order Domain, OrderPersistenceModel Model)> _tracked = new();

    public EfOrderRepository(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var model = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (model is null)
        {
            return null;
        }

        var domain = OrderMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        var model = OrderMapper.ToPersistence(order);
        await _dbContext.Orders.AddAsync(model, cancellationToken);
        _tracked[order.Id] = (order, model);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            OrderMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
