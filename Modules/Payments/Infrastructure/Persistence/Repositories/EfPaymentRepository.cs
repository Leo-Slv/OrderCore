using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.DTOs;
using OrderCore.Api.Modules.Payments.Domain.Entities;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Repositories;

/// <summary>
/// See EfCustomerRepository's remarks: reconciles the domain aggregate
/// against its tracked persistence model right before
/// <see cref="SaveChangesAsync"/>. Every Payments use case only ever
/// touches one Payment per operation, so — unlike Inventory — this keeps
/// its own `SaveChangesAsync` instead of needing an `IUnitOfWork`.
/// </summary>
public sealed class EfPaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _dbContext;
    private readonly Dictionary<Guid, (Payment Domain, PaymentPersistenceModel Model)> _tracked = new();

    public EfPaymentRepository(PaymentsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(p => p.OrderId == orderId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<IReadOnlyList<Payment>> ListByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var models = await Query().AsNoTracking().Where(p => orderIds.Contains(p.OrderId)).ToListAsync(cancellationToken);
        return models.Select(PaymentMapper.ToDomain).ToList();
    }

    public async Task<(IReadOnlyList<Payment> Items, int TotalCount)> ListAsync(ListPaymentsFilter filter, CancellationToken cancellationToken)
    {
        var query = _dbContext.Payments.AsNoTracking();

        if (filter.Status is { } status)
        {
            var statusName = status.ToString();
            query = query.Where(p => p.Status == statusName);
        }

        if (filter.Method is { } method)
        {
            var methodName = method.ToString();
            query = query.Where(p => p.Method == methodName);
        }

        if (filter.CreatedFrom is { } from)
        {
            query = query.Where(p => p.CreatedAt >= from);
        }

        if (filter.CreatedTo is { } to)
        {
            query = query.Where(p => p.CreatedAt < to);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Read-only: the list is never saved back, so the page isn't tracked.
        var models = await query
            .Include(p => p.Refunds)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (models.Select(PaymentMapper.ToDomain).ToList(), totalCount);
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        var model = PaymentMapper.ToPersistence(payment);
        await _dbContext.Payments.AddAsync(model, cancellationToken);
        _tracked[payment.Id] = (payment, model);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            PaymentMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Payment Track(PaymentPersistenceModel model)
    {
        var domain = PaymentMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    private IQueryable<PaymentPersistenceModel> Query() => _dbContext.Payments.Include(p => p.Refunds);
}
