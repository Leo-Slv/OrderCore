namespace OrderCore.Api.Modules.Customers.Application.DTOs;

/// <summary>
/// No password: credentials are the Identity module's concern
/// (<c>UserAccount</c>), which creates the customer through
/// <c>RegisterCustomerUseCase</c> during sign-up and links to it by id.
/// </summary>
public sealed record RegisterCustomerCommand(
    string Name,
    string Email,
    string? Phone,
    string? DocumentNumber);
