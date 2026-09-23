namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

public sealed class OrderResponse
{
    public Guid Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = string.Empty;

    public IReadOnlyList<OrderItemResponse> Items { get; init; } = [];
}
