using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class AddCustomerAddressUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly TimeProvider _timeProvider;

    public AddCustomerAddressUseCase(ICustomerRepository customers, TimeProvider timeProvider)
    {
        _customers = customers;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> ExecuteAsync(AddCustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{command.CustomerId}' was not found.");

        var address = CustomerAddress.Create(
            command.Label, command.RecipientName, command.Phone, command.Address, _timeProvider.GetUtcNow());

        customer.AddAddress(address);
        await _customers.SaveChangesAsync(cancellationToken);

        return address.Id;
    }
}
