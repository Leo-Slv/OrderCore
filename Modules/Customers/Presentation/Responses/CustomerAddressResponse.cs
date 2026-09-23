namespace OrderCore.Api.Modules.Customers.Presentation.Responses;

public sealed class CustomerAddressResponse
{
    public Guid Id { get; init; }

    public string Label { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    public bool IsDefaultShipping { get; init; }
}
