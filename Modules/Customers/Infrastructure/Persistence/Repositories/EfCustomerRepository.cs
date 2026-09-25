using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Mappers;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Repositories;

/// <summary>
/// ICustomerRepository has no explicit UpdateAsync (matches
/// 02-customers.md and IOrderRepository's shape): a use case is expected to
/// mutate the <see cref="Customer"/> it got from GetByIdAsync/GetByEmailAsync
/// and just call SaveChangesAsync. Since the domain aggregate and its
/// <see cref="CustomerPersistenceModel"/> are separate object graphs (the
/// project's Persistence Model separation), this repository remembers which
/// model backs each domain instance it handed out, and reconciles the two
/// with <c>CustomerMapper.ApplyChanges</c> right before flushing — an
/// instance-scoped unit of work, matching the Scoped DbContext/repository
/// lifetime.
/// </summary>
public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly CustomersDbContext _dbContext;
    private readonly Dictionary<Guid, (Customer Domain, CustomerPersistenceModel Model)> _tracked = new();

    public EfCustomerRepository(CustomersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var model = await Query().FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
        return model is null ? null : Track(model);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        var model = CustomerMapper.ToPersistence(customer);
        await _dbContext.Customers.AddAsync(model, cancellationToken);
        _tracked[customer.Id] = (customer, model);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var (domain, model) in _tracked.Values)
        {
            CustomerMapper.ApplyChanges(domain, model);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private Customer Track(CustomerPersistenceModel model)
    {
        var domain = CustomerMapper.ToDomain(model);
        _tracked[domain.Id] = (domain, model);
        return domain;
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        string? searchTerm, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Email, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var models = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Addresses)
            .Include(c => c.PaymentMethods)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (models.Select(CustomerMapper.ToDomain).ToList(), totalCount);
    }

    public async Task<IReadOnlyList<Customer>> ListByIdsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken)
    {
        var models = await Query().AsNoTracking().AsSplitQuery().Where(c => customerIds.Contains(c.Id)).ToListAsync(cancellationToken);
        return models.Select(CustomerMapper.ToDomain).ToList();
    }

    public Task<int> CountCreatedBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        _dbContext.Customers.CountAsync(c => c.CreatedAt >= from && c.CreatedAt < to, cancellationToken);

    private IQueryable<CustomerPersistenceModel> Query() =>
        _dbContext.Customers.Include(c => c.Addresses).Include(c => c.PaymentMethods);
}
