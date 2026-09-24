namespace OrderCore.Api.Shared.Presentation.Authentication;

/// <summary>
/// Claim names OrderCore puts in its access tokens (issued by the Identity
/// module) and reads back through <see cref="HttpContextCurrentUser"/>.
/// Short JWT-style names on purpose: the JWT handler is configured not to
/// map them to the long WS-Federation URIs.
/// </summary>
public static class OrderCoreClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string CustomerId = "customer_id";
}
