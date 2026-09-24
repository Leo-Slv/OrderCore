using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// Makes an address the default for shipping or billing; the previous
/// default of that kind stops being one (the aggregate enforces it).
/// </summary>
public sealed class SetDefaultAddressUseCase
{
    private readonly ICustomerRepository _customers;

    public SetDefaultAddressUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task ExecuteAsync(Guid customerId, Guid addressId, DefaultAddressKind kind, CancellationToken cancellationToken)
    {
        var customer = await CustomerAddressLookup.LoadOwnerAsync(_customers, customerId, addressId, cancellationToken);

        if (kind == DefaultAddressKind.Shipping)
        {
            customer.SetDefaultShippingAddress(addressId);
        }
        else
        {
            customer.SetDefaultBillingAddress(addressId);
        }

        await _customers.SaveChangesAsync(cancellationToken);
    }
}
