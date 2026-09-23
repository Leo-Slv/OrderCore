using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.Api.Modules.Customers.Application.Contracts;

/// <summary>
/// Persistence abstraction the Application layer depends on. The concrete
/// EF Core implementation lives in Modules/Customers/Infrastructure/Persistence
/// — see 02-customers.md and IOrderRepository's remarks on why this isn't a
/// generic <c>IRepository&lt;T&gt;</c>.
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken);

    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
