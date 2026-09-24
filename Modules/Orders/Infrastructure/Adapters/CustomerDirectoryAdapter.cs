using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Orders.Application.Contracts;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Infrastructure.Adapters;

/// <summary>
/// Implements Orders' <see cref="ICustomerDirectory"/> by calling
/// Customers' <see cref="GetCustomerAddressUseCase"/>. Same section 7
/// indirection as the other adapters here. Only the address value object
/// (shared kernel) crosses the boundary.
/// </summary>
public sealed class CustomerDirectoryAdapter : ICustomerDirectory
{
    private readonly GetCustomerAddressUseCase _getCustomerAddress;

    public CustomerDirectoryAdapter(GetCustomerAddressUseCase getCustomerAddress)
    {
        _getCustomerAddress = getCustomerAddress;
    }

    public async Task<Address> GetAddressAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customerAddress = await _getCustomerAddress.ExecuteAsync(customerId, addressId, cancellationToken);
        return customerAddress.Address;
    }
}
