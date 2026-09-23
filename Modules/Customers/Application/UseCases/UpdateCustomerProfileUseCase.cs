using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class UpdateCustomerProfileUseCase
{
    private readonly ICustomerRepository _customers;

    public UpdateCustomerProfileUseCase(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerOutput> ExecuteAsync(Guid customerId, string name, string? phone, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(customerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{customerId}' was not found.");

        customer.UpdateProfile(name, phone, customer.DocumentNumber);
        await _customers.SaveChangesAsync(cancellationToken);

        return new CustomerOutput(customer.Id, customer.Name, customer.Email, customer.Active);
    }
}
