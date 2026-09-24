namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

/// <summary>
/// One row of <c>GET orders/customers/{customerId}</c>.
/// <see cref="ItemCount"/> counts units, not lines.
/// </summary>
public sealed class OrderSummaryResponse
{
    public Guid Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public int ItemCount { get; init; }
}
