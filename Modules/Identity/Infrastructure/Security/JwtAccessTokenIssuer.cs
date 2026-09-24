using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs with the claims
/// <c>HttpContextCurrentUser</c> reads back (<see cref="OrderCoreClaimTypes"/>).
/// <c>customer_id</c> is only present for customer accounts.
/// </summary>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public AccessToken Issue(UserAccount account, DateTimeOffset now)
    {
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(OrderCoreClaimTypes.UserId, account.Id.ToString()),
            new(OrderCoreClaimTypes.Email, account.Email),
            new(OrderCoreClaimTypes.Role, account.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (account.CustomerId is { } customerId)
        {
            claims.Add(new Claim(OrderCoreClaimTypes.CustomerId, customerId.ToString()));
        }

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expiresAt);
    }

    /// <summary>Shared with the JWT bearer validation so issuing and validating can't drift apart.</summary>
    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));
}
