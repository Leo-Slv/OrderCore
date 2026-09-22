using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Customers.Domain.Entities;

/// <summary>
/// Minimal Customer aggregate. Intentionally kept small for the initial
/// scaffold — extend it only when a real business rule (invariant,
/// behavior) justifies it, per section 38 of the project context.
/// </summary>
public sealed class Customer : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    private Customer()
    {
    }

    private Customer(Guid id, string name, string email) : base(id)
    {
        Name = name;
        Email = email;
    }

    public static Customer Create(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var customer = new Customer(Guid.NewGuid(), name, email);
        customer.IncrementVersion();
        return customer;
    }
}
