using System.Text;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. <see cref="SigningKey"/>
/// is a secret: it comes from the environment (<c>Jwt__SigningKey</c>) or
/// user-secrets, never from a committed settings file.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs at least a 256-bit key.</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "OrderCore";

    public string Audience { get; set; } = "OrderCore";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    public bool HasValidSigningKey => Encoding.UTF8.GetByteCount(SigningKey) >= MinimumSigningKeyBytes;
}
