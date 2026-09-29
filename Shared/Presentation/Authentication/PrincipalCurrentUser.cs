using System.Security.Claims;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Shared.Presentation.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> read from a given principal — the one place
/// the token's claims are turned into "who is calling". Used by
/// <see cref="HttpContextCurrentUser"/> for requests and by SignalR hubs,
/// which get their principal from the connection (<c>Context.User</c>)
/// rather than from <c>IHttpContextAccessor</c>. An unauthenticated
/// principal is nobody; a claim that isn't a valid GUID counts as absent.
/// </summary>
public sealed class PrincipalCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public PrincipalCurrentUser(ClaimsPrincipal? principal)
    {
        _principal = principal is { Identity.IsAuthenticated: true } ? principal : null;
    }

    public Guid? UserId => ReadGuid(OrderCoreClaimTypes.UserId);

    public Guid? CustomerId => ReadGuid(OrderCoreClaimTypes.CustomerId);

    public string? Role => _principal?.FindFirstValue(OrderCoreClaimTypes.Role);

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(_principal?.FindFirstValue(claimType), out var value) ? value : null;
}
