using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Notifications.Application.Contracts;
using OrderCore.Api.Modules.Notifications.Application.Telemetry;
using OrderCore.Api.Modules.Notifications.Application.UseCases;
using OrderCore.Api.Modules.Notifications.Domain.Policies;
using OrderCore.Api.Modules.Notifications.Domain.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Jobs;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders.Resend;
using OrderCore.Api.Modules.Notifications.Infrastructure.Senders.Smtp;
using OrderCore.Api.Modules.Notifications.Infrastructure.Templates;
using OrderCore.Api.Shared.Infrastructure.Persistence;

namespace OrderCore.Api.Modules.Notifications;

/// <summary>
/// Registers the Notifications module (Docs/specs/identity/password-recovery.md,
/// decision 6): the e-mail queue, the templates, the sender (Resend or SMTP)
/// and the dispatcher that sends what is queued.
/// </summary>
public static class NotificationsDependencyInjection
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabaseMigrations<NotificationsDbContext>(order: 90);
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));
        services.AddScoped<IEmailMessageRepository, EfEmailMessageRepository>();

        services.AddSingleton<NotificationsMetrics>();
        services.AddSingleton<IEmailTemplates, EmbeddedEmailTemplates>();

        // E-mail that could never go out stops the API at startup.
        services.AddOptions<NotificationsOptions>().Bind(configuration.GetSection(NotificationsOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<NotificationsOptions>, NotificationsOptionsValidator>();
        services.AddOptions<ResendOptions>().Bind(configuration.GetSection(ResendOptions.SectionName));
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddHttpClient(ResendEmailSender.HttpClientName);
        services.AddSingleton<ResendEmailSender>();
        services.AddSingleton<SmtpEmailSender>();

        // Resend when its key is configured, SMTP (Mailpit locally) otherwise (spec decision 1).
        services.AddSingleton<IEmailSender>(provider =>
        {
            var useResend = provider.GetRequiredService<IOptions<ResendOptions>>().Value.IsEnabled;
            IEmailSender selected = useResend
                ? provider.GetRequiredService<ResendEmailSender>()
                : provider.GetRequiredService<SmtpEmailSender>();
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("OrderCore.Notifications")
                .LogInformation("E-mail sender: {EmailSender}.", selected.Name);
            return selected;
        });

        services.AddOptions<EmailDispatcherOptions>().Bind(configuration.GetSection(EmailDispatcherOptions.SectionName));
        services.AddSingleton(provider =>
        {
            var delays = provider.GetRequiredService<IOptions<EmailDispatcherOptions>>().Value.RetryDelays;
            return delays.Length == 0 ? EmailRetryPolicy.Default : new EmailRetryPolicy(delays);
        });

        services.AddScoped<QueueEmailUseCase>();
        services.AddScoped<SendEmailUseCase>();
        services.AddScoped<PurgeFinishedEmailsUseCase>();
        services.AddHostedService<EmailDispatcherBackgroundService>();

        return services;
    }
}
