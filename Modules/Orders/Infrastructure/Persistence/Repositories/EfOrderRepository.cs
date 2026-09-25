using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Modules.Orders.Domain.Entities;
using OrderCore.Api.Modules.Orders.Domain.Enums;
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

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListByCustomerIdAsync(
        Guid customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.Orders.Where(o => o.CustomerId == customerId);
        var totalCount = await query.CountAsync(cancellationToken);

        var models = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Include(o => o.Items)
            .AsSplitQuery()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(Track).ToList(), totalCount);
    }

    public async Task<Order?> FindByCheckoutIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(
            o => o.CustomerId == customerId && o.CheckoutIdempotencyKey == idempotencyKey, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(ListOrdersFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Orders.AsNoTracking();

        if (filter.Status is { } status)
        {
            var statusName = status.ToString();
            query = query.Where(o => o.Status == statusName);
        }

        if (filter.CustomerId is { } customerId)
        {
            query = query.Where(o => o.CustomerId == customerId);
        }

        if (filter.CreatedFrom is { } from)
        {
            query = query.Where(o => o.CreatedAt >= from);
        }

        if (filter.CreatedTo is { } to)
        {
            query = query.Where(o => o.CreatedAt < to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .Include(o => o.Items)
            .AsSplitQuery()
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(OrderMapper.ToDomain).ToList(), totalCount);
    }

    public async Task<IReadOnlyDictionary<OrderStatus, int>> CountByStatusAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var counts = await _dbContext.Orders
            .Where(o => o.CreatedAt >= from && o.CreatedAt < to)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Enum.GetValues<OrderStatus>().ToDictionary(
            status => status,
            status => counts.FirstOrDefault(c => c.Status == status.ToString())?.Count ?? 0);
    }

    /// <summary>
    /// The total isn't stored (it is computed from the items, like
    /// <c>Order.TotalAmount</c>), so each order's total is computed in SQL and
    /// the per-currency sums here — one row per order in the period, which
    /// is fine for a dashboard computed on demand.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, decimal>> SumConfirmedTotalsAsync(
        DateTimeOffset from, DateTimeOffset to, IReadOnlyCollection<OrderStatus> statuses, CancellationToken cancellationToken)
    {
        var statusNames = statuses.Select(s => s.ToString()).ToList();

        var totals = await _dbContext.Orders
            .Where(o => o.ConfirmedAt >= from && o.ConfirmedAt < to && statusNames.Contains(o.Status))
            .Select(o => new
            {
                o.Currency,
                Total = o.Items.Sum(i => (i.UnitPrice * i.Quantity) - i.DiscountAmount)
                    - o.DiscountAmount + o.ShippingAmount + o.TaxAmount,
            })
            .ToListAsync(cancellationToken);

        return totals.GroupBy(t => t.Currency).ToDictionary(g => g.Key, g => g.Sum(t => t.Total));
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
