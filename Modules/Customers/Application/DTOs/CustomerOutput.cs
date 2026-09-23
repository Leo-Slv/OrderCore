namespace OrderCore.Api.Modules.Customers.Application.DTOs;

public sealed record CustomerOutput(Guid Id, string Name, string Email, bool Active);
