namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

/// <summary>
/// Expanded beyond 05-orders.md's abbreviated Street/City-only shape to
/// every field <see cref="Shared.Domain.ValueObjects.Address.Create"/>
/// requires for both addresses — same reasoning as
/// <c>CustomerAddressRequest</c> in Customers.
/// </summary>
public sealed class SetOrderAddressesRequest
{
    public string ShippingStreet { get; init; } = string.Empty;

    public string ShippingNumber { get; init; } = string.Empty;

    public string? ShippingComplement { get; init; }

    public string ShippingNeighborhood { get; init; } = string.Empty;

    public string ShippingCity { get; init; } = string.Empty;

    public string ShippingState { get; init; } = string.Empty;

    public string ShippingPostalCode { get; init; } = string.Empty;

    public string ShippingCountry { get; init; } = string.Empty;

    public string BillingStreet { get; init; } = string.Empty;

    public string BillingNumber { get; init; } = string.Empty;

    public string? BillingComplement { get; init; }

    public string BillingNeighborhood { get; init; } = string.Empty;

    public string BillingCity { get; init; } = string.Empty;

    public string BillingState { get; init; } = string.Empty;

    public string BillingPostalCode { get; init; } = string.Empty;

    public string BillingCountry { get; init; } = string.Empty;
}
