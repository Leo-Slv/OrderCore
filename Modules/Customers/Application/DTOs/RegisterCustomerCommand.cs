namespace OrderCore.Api.Modules.Customers.Application.DTOs;

/// <summary>
/// <see cref="PasswordHash"/> is not in 02-customers.md's command shape,
/// but <c>Customer.Create</c> requires one and OrderCore has no auth module
/// yet to source it from elsewhere (section 32) — the caller is expected to
/// hash the raw password before it reaches this command.
/// </summary>
public sealed record RegisterCustomerCommand(
    string Name,
    string Email,
    string? Phone,
    string? DocumentNumber,
    string PasswordHash);
