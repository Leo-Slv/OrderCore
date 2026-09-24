using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.UnitTests.Identity;

/// <summary>Tokens "refresh-1", "refresh-2", ...; the hash is the token with an "h:" prefix.</summary>
internal sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
{
    private int _next = 1;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromDays(14);

    public GeneratedRefreshToken Generate()
    {
        var token = $"refresh-{_next++}";
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token) => "h:" + token;
}
