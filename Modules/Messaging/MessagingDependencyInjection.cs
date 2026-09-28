using Microsoft.EntityFrameworkCore;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Infrastructure.Consumers;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Modules.Messaging.Infrastructure.Relay;
using OrderCore.Api.Shared.Infrastructure.Messaging;

namespace OrderCore.Api.Modules.Messaging;

/// <summary>
/// Registers the Messaging module (technical, like AuditLogs and Identity):
/// the RabbitMQ connection and topology, the outbox relay, the consumer host
/// and the failed-message table. What is published and consumed is declared
/// by each module in its own <c>&lt;Module&gt;DependencyInjection</c>
/// (<see cref="MessagingRegistrationExtensions"/>).
/// </summary>
public static class MessagingDependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OrderCoreDb")));

        // Fails the start of the API, not the first publish, when the broker
        // isn't configured (the password comes from the environment or
        // user-secrets).
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(
                options => options.IsComplete,
                "RabbitMq:Host, RabbitMq:Username and RabbitMq:Password must be set (the password from the environment or user-secrets).")
            .ValidateOnStart();
        services.AddOptions<MessagingOptions>();

        services.IntegrationEventRegistry();
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<MessageProcessor>();
        services.AddScoped<IFailedMessageRepository, EfFailedMessageRepository>();

        // Order matters: hosted services start in registration order, so the
        // broker is reachable and the topology declared before the relay
        // publishes or the consumers subscribe.
        services.AddHostedService<RabbitMqStartupService>();
        services.AddHostedService<OutboxRelayBackgroundService>();
        services.AddHostedService<ConsumerHostBackgroundService>();

        return services;
    }
}
