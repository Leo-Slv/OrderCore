using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Shared.Presentation.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> over the current request's authenticated
/// principal (read by <see cref="PrincipalCurrentUser"/>). With no request
/// (background services) or no valid token, every property is null.
/// </summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId => Current.UserId;

    public Guid? CustomerId => Current.CustomerId;

    public string? Role => Current.Role;

    private PrincipalCurrentUser Current => new(_httpContextAccessor.HttpContext?.User);
}
