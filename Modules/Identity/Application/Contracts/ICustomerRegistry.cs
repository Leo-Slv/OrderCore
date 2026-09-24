namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary>
/// How Identity reaches the Customers module during sign-up (section 7's
/// "Application Contract" indirection), implemented by
/// <c>CustomerRegistryAdapter</c> over Customers' <c>RegisterCustomerUseCase</c>.
/// Returns the new customer's id; Customers' own errors (e.g.
/// <c>email_already_registered</c>) come through unchanged.
/// </summary>
public interface ICustomerRegistry
{
    Task<Guid> RegisterAsync(string name, string email, string? phone, CancellationToken cancellationToken);
}
