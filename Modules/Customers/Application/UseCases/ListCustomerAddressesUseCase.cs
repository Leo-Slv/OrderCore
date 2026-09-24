using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class ListCustomerAddressesUseCase
{
    private readonly ICustomerRepository _customers;

    public ListCustomerAddressesUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<IReadOnlyList<CustomerAddress>> ExecuteAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");

        return customer.Addresses.ToList();
    }
}
