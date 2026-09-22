using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;

namespace OrderCore.Api.Modules.Payments;

/// <summary>
/// Registers the Payments module's own services, the same way
/// <c>CoursesDependencyInjection</c> does for the Courses module in
/// CourseCore (section 5.1) — one extension method per module, composed in
/// Program.cs.
/// </summary>
public static class PaymentsDependencyInjection
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services)
    {
        services.Configure<FakePaymentProviderOptions>(_ => { });
        services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

        return services;
    }
}
