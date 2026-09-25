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

    /// <summary>
    /// One page of customers, newest first, optionally only those whose name
    /// or e-mail contains <paramref name="searchTerm"/> (case-insensitive).
    /// Read-only.
    /// </summary>
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(
        string? searchTerm, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Read-only; customers that don't exist are simply absent.</summary>
    Task<IReadOnlyList<Customer>> ListByIdsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>Customers created in <c>[from, to)</c>.</summary>
    Task<int> CountCreatedBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
