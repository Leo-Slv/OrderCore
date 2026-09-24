using OrderCore.Api.Modules.Customers.Application.DTOs;
using OrderCore.Api.Modules.Customers.Application.UseCases;
using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Adapters;

/// <summary>
/// Implements Identity's <see cref="ICustomerRegistry"/> by calling
/// Customers' <see cref="RegisterCustomerUseCase"/>. This is the section 7
/// "Application Contract" indirection: only Customers' Application layer is
/// referenced, and only the new customer's id comes back.
/// </summary>
public sealed class CustomerRegistryAdapter : ICustomerRegistry
{
    private readonly RegisterCustomerUseCase _registerCustomer;

    public CustomerRegistryAdapter(RegisterCustomerUseCase registerCustomer)
    {
        _registerCustomer = registerCustomer;
    }

    public async Task<Guid> RegisterAsync(string name, string email, string? phone, CancellationToken cancellationToken)
    {
        var customer = await _registerCustomer.ExecuteAsync(
            new RegisterCustomerCommand(name, email, phone, DocumentNumber: null), cancellationToken);
        return customer.Id;
    }
}
