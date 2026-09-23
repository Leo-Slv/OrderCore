namespace OrderCore.Api.Modules.Customers.Infrastructure.Persistence.Models;

/// <summary>
/// 02-customers.md's abbreviated version of this model only lists
/// Street/City/State/PostalCode; expanded here to every
/// <see cref="Shared.Domain.ValueObjects.Address"/> field (Number,
/// Complement, Neighborhood, Country) so <c>CustomerMapper</c> can actually
/// round-trip a valid <c>Address</c> back out of the database.
/// </summary>
public sealed class CustomerAddressPersistenceModel
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public string Label { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Street { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string? Complement { get; set; }

    public string Neighborhood { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public bool IsDefaultShipping { get; set; }

    public bool IsDefaultBilling { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public CustomerPersistenceModel Customer { get; set; } = null!;
}
