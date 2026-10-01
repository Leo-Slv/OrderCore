using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Notifications.Infrastructure.Adapters;

/// <summary>
/// Implements <see cref="ICustomerContacts"/> by calling Customers'
/// <see cref="GetCustomerByIdUseCase"/>: only the name and address come back.
/// </summary>
public sealed class CustomerContactsAdapter : ICustomerContacts
{
    private readonly GetCustomerByIdUseCase _getCustomerById;

    public CustomerContactsAdapter(GetCustomerByIdUseCase getCustomerById)
    {
        _getCustomerById = getCustomerById;
    }

    public async Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _getCustomerById.ExecuteAsync(customerId, cancellationToken);
            return new CustomerContact(customer.Name, customer.Email);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }
}
