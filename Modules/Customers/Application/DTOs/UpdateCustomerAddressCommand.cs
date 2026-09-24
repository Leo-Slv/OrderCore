using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Customers.Application.DTOs;

public sealed record UpdateCustomerAddressCommand(
    Guid CustomerId, Guid AddressId, string Label, string RecipientName, string? Phone, Address Address);
