using OrderCore.Api.Shared.Domain.ValueObjects;

namespace OrderCore.Api.Modules.Customers.Application.DTOs;

/// <summary>
/// <see cref="Phone"/> is not in 02-customers.md's command shape, but
/// <c>CustomerAddress.Create</c> takes an optional phone — added as
/// nullable so the field is actually reachable from the API.
/// </summary>
public sealed record AddCustomerAddressCommand(Guid CustomerId, string Label, string RecipientName, string? Phone, Address Address);
