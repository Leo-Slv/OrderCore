using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Models;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Persistence.Repositories;

/// <summary>
/// See EfCustomerRepository's remarks: reconciles the domain aggregate
/// against its tracked persistence model right before
/// <see cref="SaveChangesAsync"/>, since IOrderRepository has no explicit
/// UpdateAsync either. Orders only ever touches one aggregate root per
/// operation, so — unlike Inventory — no <c>IUnitOfWork</c> is needed:
/// this repository dispatches domain events itself, right after its own
/// save succeeds, per 05-orders.md's
/// <c>EfOrderRepository --&gt; IDomainEventDispatcher</c>.
/// </summary>
public sealed class EfOrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _dbContext;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly Dictionary<Guid, (Order Domain, OrderPersistenceModel Model)> _tracked = new();

    public EfOrderRepository(OrdersDbContext dbContext, IDomainEventDispatcher domainEventDispatcher)
    {
        _dbContext = dbContext;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<Order>> ListByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var models = await Query().Where(o => o.CustomerId == customerId).ToListAsync(cancellationToken);
        return models.Select(Track).ToList();
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

        var events = _tracked.Values.SelectMany(t => t.Domain.DomainEvents).ToList();
        if (events.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(events, cancellationToken);

            foreach (var (domain, _) in _tracked.Values)
            {
                domain.ClearDomainEvents();
            }
        }
    }

    private Order Track(OrderPersistenceModel model)
    {
        var domain = OrderMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    private IQueryable<OrderPersistenceModel> Query() => _dbContext.Orders.Include(o => o.Items);
}
