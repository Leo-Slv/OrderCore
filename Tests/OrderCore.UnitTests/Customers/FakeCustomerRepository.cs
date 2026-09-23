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
}
