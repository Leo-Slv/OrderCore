namespace OrderCore.Api.Modules.Orders.Presentation.Requests;

public sealed class CreateOrderRequest
{
    public Guid CustomerId { get; init; }

    public string Currency { get; init; } = string.Empty;

    public IReadOnlyCollection<CreateOrderItemRequest> Items { get; init; } = [];
}
