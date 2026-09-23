namespace OrderCore.Api.Modules.Orders.Application.DTOs;

public sealed record CancelOrderCommand(Guid OrderId, string Reason);
