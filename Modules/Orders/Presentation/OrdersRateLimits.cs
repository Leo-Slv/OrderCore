using OrderCore.Api.Shared.Presentation.RateLimiting;

namespace OrderCore.Api.Modules.Orders.Presentation;

/// <summary>
/// Checkout reserves stock and starts a payment, so it is limited per
/// customer (production-readiness spec, decision 4). <c>RateLimits:Checkout</c>.
/// </summary>
public static class OrdersRateLimits
{
    public const string Checkout = "Checkout";

    public static IServiceCollection AddOrdersRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.AddFixedWindowPolicy(configuration, Checkout, permitLimit: 10, window: TimeSpan.FromMinutes(1), perCustomer: true);
}
