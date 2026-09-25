namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>The customer behind an order. <see cref="Active"/> is false for a customer an admin deactivated.</summary>
public sealed class OrderCustomerResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool Active { get; init; }
}
