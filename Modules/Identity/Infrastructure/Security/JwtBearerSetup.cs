using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Realtime;

namespace OrderCore.Api.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Validates the access tokens <see cref="JwtAccessTokenIssuer"/> issues,
/// with the same <see cref="JwtOptions"/>, so issuing and validating can't
/// drift apart. Claim names are kept exactly as issued (no mapping to the
/// long WS-Federation URIs), which is what <c>HttpContextCurrentUser</c>
/// and the role-based policies read.
/// </summary>
public static class JwtBearerSetup
{
    public static IServiceCollection AddOrderCoreJwtBearer(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(jwt),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = OrderCoreClaimTypes.UserId,
                    RoleClaimType = OrderCoreClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // SignalR (WebSockets/SSE) can't send the header from a browser: accept
                // the token from the query string, only on hub paths.
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.HttpContext.Request.Path.StartsWithSegments(HubRoutes.Prefix)
                            && context.Request.Query[HubRoutes.AccessTokenQueryParameter] is { Count: > 0 } token)
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
