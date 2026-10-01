namespace OrderCore.Api.Modules.Notifications.Application.Contracts;

public sealed record CustomerContact(string Name, string Email);

/// <summary>
/// Where to send a customer's order e-mails (section 7's "Application
/// Contract" indirection), implemented by an adapter over the Customers
/// module. Null when the customer no longer exists.
/// </summary>
public interface ICustomerContacts
{
    Task<CustomerContact?> GetAsync(Guid customerId, CancellationToken cancellationToken);
}
