using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Shared.Application.Exceptions;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Adapters;

/// <summary>
/// Implements Identity's <see cref="ICustomerRegistry"/> by calling
/// Customers' use cases. This is the section 7 "Application Contract"
/// indirection: only Customers' Application layer is referenced, and only
/// an id or a yes/no comes back.
/// </summary>
public sealed class CustomerRegistryAdapter : ICustomerRegistry
{
    private readonly RegisterCustomerUseCase _registerCustomer;
    private readonly GetCustomerByIdUseCase _getCustomerById;

    public CustomerRegistryAdapter(RegisterCustomerUseCase registerCustomer, GetCustomerByIdUseCase getCustomerById)
    {
        _registerCustomer = registerCustomer;
        _getCustomerById = getCustomerById;
    }

    public async Task<Guid> RegisterAsync(string name, string email, string? phone, CancellationToken cancellationToken)
    {
        var customer = await _registerCustomer.ExecuteAsync(
            new RegisterCustomerCommand(name, email, phone, DocumentNumber: null), cancellationToken);
        return customer.Id;
    }

    public async Task<bool> IsActiveAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            return (await _getCustomerById.ExecuteAsync(customerId, cancellationToken)).Active;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }
}
