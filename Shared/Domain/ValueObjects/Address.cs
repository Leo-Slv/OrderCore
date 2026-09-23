namespace OrderCore.Api.Shared.Domain.ValueObjects;

/// <summary>
/// Physical address, shared by <c>CustomerAddress</c> (Customers) and
/// <c>Order.ShippingAddress</c>/<c>Order.BillingAddress</c> (Orders) — see
/// 01-shared-kernel.md. <see cref="Complement"/> is the only optional part
/// (e.g. apartment/suite number).
/// </summary>
public sealed record Address
{
    public string Street { get; }

    public string Number { get; }

    public string? Complement { get; }

    public string Neighborhood { get; }

    public string City { get; }

    public string State { get; }

    public string PostalCode { get; }

    public string Country { get; }

    private Address(
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string country)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }

    public static Address Create(
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string country)
    {
        RequireNonBlank(street, nameof(street));
        RequireNonBlank(number, nameof(number));
        RequireNonBlank(neighborhood, nameof(neighborhood));
        RequireNonBlank(city, nameof(city));
        RequireNonBlank(state, nameof(state));
        RequireNonBlank(postalCode, nameof(postalCode));
        RequireNonBlank(country, nameof(country));

        return new Address(
            street,
            number,
            string.IsNullOrWhiteSpace(complement) ? null : complement,
            neighborhood,
            city,
            state,
            postalCode,
            country);
    }

    private static void RequireNonBlank(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }
    }
}
