namespace OrderCore.Api.Modules.Orders.Application.DTOs;

public sealed record CreateOrderItem(Guid ProductId, int Quantity);

public sealed record CreateOrderCommand(Guid CustomerId, string Currency, IReadOnlyCollection<CreateOrderItem> Items);

public sealed record CreateOrderResult(Guid OrderId, decimal TotalAmount, string Status);
