using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Application.Contracts;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Security;

/// <summary>
/// 256 random bits, base64url-encoded, for the client; a hex SHA-256 of it
/// for storage. A plain hash (not a slow password hash) is enough here:
/// the token has full entropy, and lookups must be by exact hash.
/// </summary>
public sealed class RandomRefreshTokenGenerator : IRefreshTokenGenerator
{
    private readonly JwtOptions _options;

    public RandomRefreshTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan Lifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public GeneratedRefreshToken Generate()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
