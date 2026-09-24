using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// Loads a customer and checks that the address is theirs, for the use
/// cases that change a saved address. Someone else's address id is "not
/// found" (404) here, not the domain's 400 "does not belong", so a caller
/// can't tell whether an address exists for another customer.
/// </summary>
internal static class CustomerAddressLookup
{
    public static async Task<Customer> LoadOwnerAsync(
        ICustomerRepository customers, Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");

        if (customer.Addresses.All(a => a.Id != addressId))
        {
            throw new NotFoundException("address_not_found", $"Address '{addressId}' was not found for customer '{customerId}'.");
        }

        return customer;
    }
}
