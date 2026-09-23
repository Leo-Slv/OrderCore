using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Orders.Application.DTOs;

public sealed record SetOrderAddressesCommand(Guid OrderId, Address ShippingAddress, Address BillingAddress);
