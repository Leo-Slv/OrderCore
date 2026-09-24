namespace OrderCore.Api.Modules.Identity.Presentation.Responses;

/// <summary>
/// Send <see cref="AccessToken"/> as <c>Authorization: Bearer ...</c> until
/// <see cref="AccessTokenExpiresAt"/>, then exchange <see cref="RefreshToken"/>
/// at <c>auth/refresh</c> for a new pair. The old refresh token stops
/// working, and presenting it again ends the session. Keep the refresh
/// token out of JavaScript's reach (e.g. an httpOnly cookie set by the
/// frontend's own server).
/// </summary>
public sealed class AuthTokensResponse
{
    public Guid UserId { get; init; }

    /// <summary><c>Customer</c> or <c>Admin</c>.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Set for customers only.</summary>
    public Guid? CustomerId { get; init; }

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAt { get; init; }

    public string RefreshToken { get; init; } = string.Empty;

    public DateTimeOffset RefreshTokenExpiresAt { get; init; }
}
