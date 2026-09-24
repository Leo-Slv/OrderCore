using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

/// <summary>
/// Resolves one of a customer's saved addresses, which is what checkout
/// needs to turn a chosen address id into the address it ships to.
/// An inactive customer can't buy, so their addresses are refused here
/// (<c>customer_inactive</c>) rather than handed to a checkout that would
/// create an order for them. An address id belonging to a different
/// customer is "not found", the same as an id that doesn't exist.
/// </summary>
public sealed class GetCustomerAddressUseCase
{
    private readonly ICustomerRepository _customers;

    public GetCustomerAddressUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerAddress> ExecuteAsync(Guid customerId, Guid addressId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");

        if (!customer.Active)
        {
            throw new DomainRuleViolationException("customer_inactive", $"Customer '{customerId}' is inactive.");
        }

        return customer.Addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new NotFoundException("address_not_found", $"Address '{addressId}' was not found for customer '{customerId}'.");
    }
}
