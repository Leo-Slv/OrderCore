using OrderCore.Api.Shared.Presentation.RateLimiting;

namespace OrderCore.Api.Modules.Identity.Presentation;

/// <summary>
/// Limits on the anonymous account endpoints, per client address — against
/// password guessing, mass sign-ups (production-readiness spec, decision
/// 4) and using the reset form to flood someone's inbox or guess tokens
/// (password-recovery plan, stage 2). Values under <c>RateLimits:&lt;Policy&gt;</c>.
/// </summary>
public static class IdentityRateLimits
{
    public const string SignIn = "SignIn";
    public const string SignUp = "SignUp";
    public const string Refresh = "Refresh";
    public const string ForgotPassword = "ForgotPassword";
    public const string ResetPassword = "ResetPassword";

    public static IServiceCollection AddIdentityRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddFixedWindowPolicy(configuration, SignIn, permitLimit: 10, window: TimeSpan.FromMinutes(1))
            .AddFixedWindowPolicy(configuration, SignUp, permitLimit: 5, window: TimeSpan.FromHours(1))
            .AddFixedWindowPolicy(configuration, Refresh, permitLimit: 30, window: TimeSpan.FromMinutes(1))
            .AddFixedWindowPolicy(configuration, ForgotPassword, permitLimit: 5, window: TimeSpan.FromMinutes(15))
            .AddFixedWindowPolicy(configuration, ResetPassword, permitLimit: 10, window: TimeSpan.FromMinutes(15));
}
