using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.UnitTests.Customers;

/// <summary>
/// In-memory <see cref="ICustomerRepository"/> used only by the use case
/// tests in this folder, so they exercise real orchestration logic without
/// needing the (not yet implemented) EF Core repository.
/// </summary>
internal sealed class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = new();

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(_customers.GetValueOrDefault(customerId));

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_customers.Values.FirstOrDefault(c => c.Email == email));

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        _customers[customer.Id] = customer;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Applies the search and paging like the EF repository, newest first.</summary>
    public Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        string? searchTerm, int page, int pageSize, CancellationToken cancellationToken)
    {
        var matching = _customers.Values
            .Where(c => string.IsNullOrWhiteSpace(searchTerm)
                || c.Name.Contains(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase)
                || c.Email.Contains(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.CreatedAt)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Customer>, int)>((matching.Skip((page - 1) * pageSize).Take(pageSize).ToList(), matching.Count));
    }

    public Task<IReadOnlyList<Customer>> ListByIdsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Customer>>(_customers.Values.Where(c => customerIds.Contains(c.Id)).ToList());

    public Task<int> CountCreatedBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        Task.FromResult(_customers.Values.Count(c => c.CreatedAt >= from && c.CreatedAt < to));
}
