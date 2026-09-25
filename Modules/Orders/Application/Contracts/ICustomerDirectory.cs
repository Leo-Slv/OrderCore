using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Application.Contracts;

/// <summary>
/// How Orders reaches the Customers module (section 7's "Application
/// Contract" indirection), implemented by <c>CustomerDirectoryAdapter</c>.
/// Throws the Customers module's own not-found/inactive errors
/// (<c>customer_not_found</c>, <c>address_not_found</c>,
/// <c>customer_inactive</c>) unchanged.
/// </summary>
public interface ICustomerDirectory
{
    Task<Address> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken);

    /// <summary>Several customers in one call; unknown ids are absent.</summary>
    Task<IReadOnlyDictionary<Guid, OrderCustomerSnapshot>> GetCustomersAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>Customers who signed up in <c>[from, to)</c>.</summary>
    Task<int> CountNewCustomersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
