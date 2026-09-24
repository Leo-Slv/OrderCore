using OrderCore.Api.Modules.Customers.Application.Contracts;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// Removing the default shipping or billing address leaves the customer
/// without that default; nothing is promoted in its place.
/// </summary>
public sealed class RemoveCustomerAddressUseCase
{
    private readonly ICustomerRepository _customers;

    public RemoveCustomerAddressUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task ExecuteAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await CustomerAddressLookup.LoadOwnerAsync(_customers, customerId, addressId, cancellationToken);

        customer.RemoveAddress(addressId);
        await _customers.SaveChangesAsync(cancellationToken);
    }
}
