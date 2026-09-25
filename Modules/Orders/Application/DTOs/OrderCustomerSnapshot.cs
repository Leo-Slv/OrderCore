namespace OrderCore.Api.Modules.Orders.Application.DTOs;

/// <summary>The customer behind an order, as the backoffice shows it.</summary>
public sealed record OrderCustomerSnapshot(Guid Id, string Name, string Email, bool Active);
