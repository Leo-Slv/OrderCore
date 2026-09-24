namespace OrderCore.Api.Modules.Identity.Application.DTOs;

/// <summary>What a successful sign-up, sign-in or refresh hands back to the client.</summary>
public sealed record AuthTokens(
    Guid UserId,
    string Role,
    Guid? CustomerId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
