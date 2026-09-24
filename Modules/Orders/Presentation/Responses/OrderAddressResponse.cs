namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

public sealed class OrderAddressResponse
{
    public string Street { get; init; } = string.Empty;

    public string Number { get; init; } = string.Empty;

    public string? Complement { get; init; }

    public string Neighborhood { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;
}
