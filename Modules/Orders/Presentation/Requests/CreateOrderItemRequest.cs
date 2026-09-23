namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class CreateOrderItemRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }
}
