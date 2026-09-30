using OrderCore.Api.Shared.Presentation.RateLimiting;

namespace OrderCore.Api.Modules.Payments.Presentation;

/// <summary>
/// The Stripe webhook is anonymous and verifies a signature on every call,
/// so it is limited per address, well above Stripe's own delivery rate
/// (production-readiness spec, decision 4). <c>RateLimits:StripeWebhook</c>.
/// </summary>
public static class PaymentsRateLimits
{
    public const string StripeWebhook = "StripeWebhook";

    public static IServiceCollection AddPaymentsRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.AddFixedWindowPolicy(configuration, StripeWebhook, permitLimit: 300, window: TimeSpan.FromMinutes(1));
}
