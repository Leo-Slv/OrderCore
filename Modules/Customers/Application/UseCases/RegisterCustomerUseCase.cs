using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class RegisterCustomerUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly TimeProvider _timeProvider;

    public RegisterCustomerUseCase(ICustomerRepository customers, TimeProvider timeProvider)
    {
        _customers = customers;
        _timeProvider = timeProvider;
    }

    public async Task<CustomerOutput> ExecuteAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var existing = await _customers.GetByEmailAsync(command.Email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A customer with email '{command.Email}' is already registered.");
        }

        var now = _timeProvider.GetUtcNow();
        var customer = Customer.Create(command.Name, command.Email, command.PasswordHash, now);
        customer.UpdateProfile(command.Name, command.Phone, command.DocumentNumber);

        await _customers.AddAsync(customer, cancellationToken);
        await _customers.SaveChangesAsync(cancellationToken);

        return new CustomerOutput(customer.Id, customer.Name, customer.Email, customer.Active);
    }
}
