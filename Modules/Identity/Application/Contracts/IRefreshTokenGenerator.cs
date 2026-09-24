namespace OrderCore.Api.Modules.Identity.Application.Contracts;

/// <summary><see cref="Token"/> goes to the client; only <see cref="Hash"/> is stored.</summary>
public sealed record GeneratedRefreshToken(string Token, string Hash);

public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    /// <summary>The hash a presented token is looked up by; the same function <see cref="Generate"/> uses.</summary>
    string Hash(string token);

    /// <summary>How long a refresh token (and so a signed-in session) lasts.</summary>
    TimeSpan Lifetime { get; }
}
