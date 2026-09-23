namespace OrderCore.Api.Modules.Customers.Presentation.Requests;

/// <summary>
/// <see cref="PasswordHash"/> is a placeholder until OrderCore has a real
/// auth module (section 32) with server-side password hashing (e.g.
/// ASP.NET Core Identity's <c>IPasswordHasher</c>) — accepting a hash
/// straight from the request body is not how this should look once that
/// module exists.
/// </summary>
public sealed class RegisterCustomerRequest
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? Phone { get; init; }

    public string? DocumentNumber { get; init; }

    public string PasswordHash { get; init; } = string.Empty;
}
