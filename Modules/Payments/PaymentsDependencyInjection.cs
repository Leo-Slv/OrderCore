using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Payments.Application.Contracts;
using OrderCore.Api.Modules.Payments.Application.UseCases;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Outbox;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using OrderCore.Api.Modules.Payments.Infrastructure.Webhooks;

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
        services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddHostedService<OutboxPublisherBackgroundService>();

        services.AddScoped<CreatePaymentUseCase>();
        services.AddScoped<AuthorizePaymentUseCase>();
        services.AddScoped<CapturePaymentUseCase>();
        services.AddScoped<FailPaymentUseCase>();
        services.AddScoped<RequestRefundUseCase>();
        services.AddScoped<GetPaymentByOrderIdUseCase>();
        services.AddScoped<GetPaymentsByOrderIdsUseCase>();
        services.AddScoped<GetPaymentByIdUseCase>();
        services.AddScoped<ListPaymentsUseCase>();
        services.AddScoped<SettlePaymentForCancellationUseCase>();

        services.AddScoped<PaymentWebhookHandler>();

        return services;
    }
}
