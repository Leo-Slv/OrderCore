namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary>
/// How Identity reaches the Customers module (section 7's "Application
/// Contract" indirection), implemented by <c>CustomerRegistryAdapter</c> over
/// Customers' use cases: to register the customer at sign-up, and to ask
/// whether a customer is still active when they sign in or refresh.
/// Customers' own errors at sign-up (e.g. <c>email_already_registered</c>)
/// come through unchanged.
/// </summary>
public interface ICustomerRegistry
{
    /// <summary>Creates the customer and returns its id.</summary>
    Task<Guid> RegisterAsync(string name, string email, string? phone, CancellationToken cancellationToken);

    /// <summary>
    /// False for a customer an admin deactivated, and for one that doesn't
    /// exist: either way their account must not get a session.
    /// </summary>
    Task<bool> IsActiveAsync(Guid customerId, CancellationToken cancellationToken);
}
