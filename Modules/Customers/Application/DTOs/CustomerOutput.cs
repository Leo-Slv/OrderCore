using OrderCore.Api.Modules.Customers.Domain.Entities;

namespace OrderCore.Api.Modules.Customers.Application.DTOs;

public sealed record CustomerOutput(Guid Id, string Name, string Email, string? Phone, bool Active, DateTimeOffset CreatedAt)
{
    public static CustomerOutput From(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.Active, customer.CreatedAt);
}
