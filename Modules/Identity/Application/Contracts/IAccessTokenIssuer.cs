using OrderCore.Api.Modules.Identity.Domain.Entities;

namespace OrderCore.Api.Modules.Identity.Application.Contracts;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Issues the short-lived signed access token (a JWT) for an account.</summary>
public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account, DateTimeOffset now);
}
