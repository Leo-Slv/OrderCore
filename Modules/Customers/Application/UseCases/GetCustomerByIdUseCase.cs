using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class GetCustomerByIdUseCase
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerOutput> ExecuteAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Customer '{customerId}' was not found.");

        return new CustomerOutput(customer.Id, customer.Name, customer.Email, customer.Active);
    }
}
