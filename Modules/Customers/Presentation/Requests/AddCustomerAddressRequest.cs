namespace OrderCore.Api.Modules.Customers.Presentation.Requests;

/// <summary>
/// Expanded beyond 02-customers.md's abbreviated
/// Street/City/State/PostalCode shape to every field
/// <see cref="Shared.Domain.ValueObjects.Address.Create"/> requires
/// (Number, Neighborhood, Country) plus the optional Complement/Phone —
/// otherwise a valid <c>Address</c> could never actually be constructed
/// from this request.
/// </summary>
public sealed class AddCustomerAddressRequest
{
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
}
