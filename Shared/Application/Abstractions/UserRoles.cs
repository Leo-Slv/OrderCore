namespace OrderCore.Api.Shared.Application.Abstractions;

/// <summary>
/// Role names as they appear in access tokens and authorization policies.
/// Shared (not owned by the Identity module) because every module's
/// endpoints are authorized against them.
/// </summary>
public static class UserRoles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
}
