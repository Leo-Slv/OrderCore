using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Customers.Domain.Entities;

/// <summary>
/// One of a <see cref="Customer"/>'s saved addresses. Owns its own default
/// flags (<see cref="MarkAsDefaultShipping"/> etc.) so that the invariant
/// "unmark the previous default before marking a new one" stays in
/// <see cref="Customer.SetDefaultShippingAddress"/>, not duplicated at
/// every call site — see 02-customers.md.
/// </summary>
public sealed class CustomerAddress : Entity<Guid>
{
    public string Label { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public Address Address { get; private set; } = null!;

    public bool IsDefaultShipping { get; private set; }

    public bool IsDefaultBilling { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private CustomerAddress()
    {
    }

    private CustomerAddress(Guid id, string label, string recipientName, string? phone, Address address, DateTimeOffset now)
        : base(id)
    {
        Label = label;
        RecipientName = recipientName;
        Phone = phone;
        Address = address;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static CustomerAddress Create(string label, string recipientName, string? phone, Address address, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label is required.", nameof(label));
        }

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        }

        ArgumentNullException.ThrowIfNull(address);

        return new CustomerAddress(Guid.NewGuid(), label, recipientName, phone, address, now);
    }

    public void UpdateContactInfo(string recipientName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        }

        RecipientName = recipientName;
        Phone = phone;
    }

    public void MarkAsDefaultShipping() => IsDefaultShipping = true;

    public void UnmarkAsDefaultShipping() => IsDefaultShipping = false;

    public void MarkAsDefaultBilling() => IsDefaultBilling = true;

    public void UnmarkAsDefaultBilling() => IsDefaultBilling = false;
}
