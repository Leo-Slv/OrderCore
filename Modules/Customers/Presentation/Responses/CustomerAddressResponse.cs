namespace OrderCore.Api.Modules.Customers.Presentation.Responses;

/// <summary>
/// A saved address in full, so checkout can show it and let the customer
/// pick it by <see cref="Id"/>.
/// </summary>
public sealed class CustomerAddressResponse
{
    public Guid Id { get; init; }

    public string Label { get; init; } = string.Empty;

    public string RecipientName { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string Street { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;

    public string? Complement { get; init; }

    public string Neighborhood { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public bool IsDefaultShipping { get; init; }

    public bool IsDefaultBilling { get; init; }
}
