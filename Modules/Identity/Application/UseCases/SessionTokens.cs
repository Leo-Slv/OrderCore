using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Domain.Entities;

namespace OrderCore.Api.Modules.Identity.Application.UseCases;

/// <summary>Builds the <see cref="AuthTokens"/> every sign-up, sign-in and refresh returns.</summary>
internal static class SessionTokens
{
    public static AuthTokens For(
        UserAccount account, string refreshToken, RefreshSession session, IAccessTokenIssuer accessTokens, DateTimeOffset now)
    {
        var accessToken = accessTokens.Issue(account, now);

        return new AuthTokens(
            account.Id,
            account.Role.ToString(),
            account.CustomerId,
            accessToken.Value,
            accessToken.ExpiresAt,
            refreshToken,
            session.ExpiresAt);
    }
}
