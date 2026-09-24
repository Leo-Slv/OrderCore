using Microsoft.AspNetCore.Authorization;
using OrderCore.Api.Shared.Application.Abstractions;

namespace OrderCore.Api.Shared.Presentation.Authentication;

/// <summary>
/// The access classes every endpoint belongs to. The fallback policy makes
/// the API deny by default: an action with no attribute at all still
/// requires a signed-in user, and <c>EndpointAuthorizationTests</c> fails
/// if any controller action isn't explicitly classified with
/// <c>[AllowAnonymous]</c>, <c>[Authorize]</c> (any signed-in user) or
/// <c>[Authorize(Policy = ...)]</c> with one of these policies.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>A signed-in customer (the token carries their customer id).</summary>
    public const string Customer = "Customer";

    public const string Admin = "Admin";

    public static IServiceCollection AddOrderCoreAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Customer, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoles.Customer)
                .RequireClaim(OrderCoreClaimTypes.CustomerId))
            .AddPolicy(Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRoles.Admin))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
