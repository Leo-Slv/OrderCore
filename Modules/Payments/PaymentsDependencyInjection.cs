using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.Telemetry;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Jobs;
using OrderCore.Api.Modules.Payments.Infrastructure.Messaging;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Stripe;
using OrderCore.Api.Modules.Payments.Infrastructure.Webhooks;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using OrderCore.Api.Shared.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Presentation;

namespace OrderCore.Api.Modules.Payments;

/// <summary>
/// Registers the Payments module's own services, the same way
/// <c>CoursesDependencyInjection</c> does for the Courses module in
/// CourseCore (section 5.1) — one extension method per module, composed in
/// Program.cs.
/// </summary>
public static class PaymentsDependencyInjection
{
    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakePaymentProviderOptions>(_ => { });
        services.AddSingleton<PaymentsMetrics>();
        services.AddPaymentsRateLimits(configuration);
        // A Stripe configuration that would run but misbehave stops the API at startup.
        services.AddOptions<StripeOptions>().Bind(configuration.GetSection(StripeOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>();
        services.AddSingleton<FakePaymentProvider>();
        services.AddHttpClient(StripePaymentProvider.HttpClientName);
        services.AddSingleton<StripePaymentProvider>();

        // Stripe when its secret key is configured, the fake otherwise (spec
        // decision 3); either way wrapped to measure every call.
        services.AddSingleton<IPaymentProvider>(provider =>
        {
            var useStripe = provider.GetRequiredService<IOptions<StripeOptions>>().Value.IsEnabled;
            IPaymentProvider selected = useStripe
                ? provider.GetRequiredService<StripePaymentProvider>()
                : provider.GetRequiredService<FakePaymentProvider>();
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("OrderCore.Payments")
                .LogInformation("Payment provider: {Provider}.", selected.Info.Name);
            return new MeasuredPaymentProvider(selected, provider.GetRequiredService<PaymentsMetrics>());
        });

        services.AddDatabaseMigrations<PaymentsDbContext>(order: 60);

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<IPaymentRepository, EfPaymentRepository>();

        // What Payments publishes, through its own outbox (Docs/specs/events).
        services.AddScoped<OutboxWriter<PaymentsDbContext>>();
        services.AddScoped<IPaymentsOutbox, PaymentsOutbox>();
        services.AddOutboxSource<PaymentsDbContext>();
        services.AddInboxSource<PaymentsDbContext>();
        services.AddIntegrationEvent<PaymentRequested>(PaymentRequested.Name, 1);
        services.AddIntegrationEvent<PaymentAuthorized>(PaymentAuthorized.Name, 1);
        services.AddIntegrationEvent<PaymentFailed>(PaymentFailed.Name, 1);
        services.AddIntegrationEvent<PaymentCaptured>(PaymentCaptured.Name, 1);
        services.AddIntegrationEvent<PaymentVoided>(PaymentVoided.Name, 1);
        services.AddIntegrationEvent<PaymentRefunded>(PaymentRefunded.Name, 1);
        services.AddIntegrationEvent<PaymentAuthorizationExpired>(PaymentAuthorizationExpired.Name, 1);

        services.AddScoped<CreatePaymentUseCase>();
        services.AddScoped<AuthorizePaymentUseCase>();
        services.AddScoped<CapturePaymentUseCase>();
        services.AddScoped<FailPaymentUseCase>();
        services.AddScoped<RequestRefundUseCase>();
        services.AddScoped<GetPaymentByOrderIdUseCase>();
        services.AddScoped<GetPaymentNextActionUseCase>();
        services.AddScoped<GetPaymentsByOrderIdsUseCase>();
        services.AddScoped<GetPaymentByIdUseCase>();
        services.AddScoped<GetAvailablePaymentMethodsUseCase>();
        services.AddScoped<ListPaymentsUseCase>();
        services.AddScoped<SettlePaymentForCancellationUseCase>();

        services.AddScoped<ApplyPaymentProviderUpdateUseCase>();
        services.AddScoped<StripeWebhookHandler>();
        services.AddScoped<CountExpiringAuthorizationsUseCase>();

        // The payment window: payments left waiting for the buyer are given up on.
        services.AddOptions<PaymentWindowOptions>().Bind(configuration.GetSection(PaymentWindowOptions.SectionName));
        services.AddScoped<ExpirePaymentWindowUseCase>();
        services.AddHostedService<PaymentWindowBackgroundService>();

        // Reconciliation: what the provider says, for payments that may have missed a webhook.
        services.AddOptions<ReconciliationOptions>().Bind(configuration.GetSection(ReconciliationOptions.SectionName));
        services.AddScoped<ReconcilePaymentUseCase>();
        services.AddHostedService<ReconciliationBackgroundService>();

        return services;
    }
}
