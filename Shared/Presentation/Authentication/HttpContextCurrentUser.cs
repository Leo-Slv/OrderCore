using System.Security.Claims;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Shared.Presentation.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> over the current request's authenticated
/// principal. With no request (background services) or no valid token,
/// every property is null. A claim that is present but not a valid GUID
/// is treated as absent rather than trusted.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => ReadGuid(OrderCoreClaimTypes.UserId);

    public Guid? CustomerId => ReadGuid(OrderCoreClaimTypes.CustomerId);

    public string? Role => Principal?.FindFirstValue(OrderCoreClaimTypes.Role);

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value) ? value : null;
}
