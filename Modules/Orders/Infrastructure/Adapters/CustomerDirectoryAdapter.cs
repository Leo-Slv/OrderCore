using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Modules.Orders.Application.DTOs;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' <see cref="ICustomerDirectory"/> by calling
/// Customers' use cases. Same section 7 indirection as the other adapters
/// here. Only the address value object (shared kernel) and Orders' own
/// <see cref="OrderCustomerSnapshot"/> cross the boundary.
/// </summary>
public sealed class CustomerDirectoryAdapter : ICustomerDirectory
{
    private readonly GetCustomerAddressUseCase _getCustomerAddress;
    private readonly GetCustomersByIdsUseCase _getCustomersByIds;
    private readonly CountNewCustomersUseCase _countNewCustomers;

    public CustomerDirectoryAdapter(
        GetCustomerAddressUseCase getCustomerAddress,
        GetCustomersByIdsUseCase getCustomersByIds,
        CountNewCustomersUseCase countNewCustomers)
    {
        _getCustomerAddress = getCustomerAddress;
        _getCustomersByIds = getCustomersByIds;
        _countNewCustomers = countNewCustomers;
    }

    public async Task<Address> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customerAddress = await _getCustomerAddress.ExecuteAsync(customerId, addressId, cancellationToken);
        return customerAddress.Address;
    }

    public async Task<IReadOnlyDictionary<Guid, OrderCustomerSnapshot>> GetCustomersAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken) =>
        (await _getCustomersByIds.ExecuteAsync(customerIds, cancellationToken))
            .ToDictionary(c => c.Id, c => new OrderCustomerSnapshot(c.Id, c.Name, c.Email, c.Active));

    public Task<int> CountNewCustomersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken) =>
        _countNewCustomers.ExecuteAsync(from, to, cancellationToken);
}
