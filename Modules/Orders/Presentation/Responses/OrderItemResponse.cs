namespace OrderCore.Api.Modules.Orders.Presentation.Responses;

public sealed class OrderItemResponse
{
    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal Total { get; init; }
}
