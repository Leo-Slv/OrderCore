using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.Api.Modules.Customers.Application.UseCases;

public sealed class UpdateCustomerAddressUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly TimeProvider _timeProvider;

    public UpdateCustomerAddressUseCase(ICustomerRepository customers, TimeProvider timeProvider)
    {
        _customers = customers;
        _timeProvider = timeProvider;
    }

    public async Task<CustomerAddress> ExecuteAsync(UpdateCustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var customer = await CustomerAddressLookup.LoadOwnerAsync(_customers, command.CustomerId, command.AddressId, cancellationToken);

        customer.UpdateAddress(
            command.AddressId, command.Label, command.RecipientName, command.Phone, command.Address, _timeProvider.GetUtcNow());
        await _customers.SaveChangesAsync(cancellationToken);

        return customer.Addresses.Single(a => a.Id == command.AddressId);
    }
}
