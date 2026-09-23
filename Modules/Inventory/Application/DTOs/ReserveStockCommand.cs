namespace OrderCore.Api.Modules.Inventory.Application.DTOs;

public sealed record ReserveStockCommand(Guid ProductId, Guid OrderId, Guid OrderItemId, int Quantity);
