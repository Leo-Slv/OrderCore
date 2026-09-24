using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderCore.Api.Modules.Payments.Application.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;

namespace OrderCore.Api.Modules.Payments.Infrastructure.Outbox;

/// <summary>
/// Polls for unprocessed <see cref="OutboxMessage"/> rows and publishes
/// them by calling <see cref="IDomainEventDispatcher.DispatchAsync"/>
/// directly — RabbitMQ (section 21) is a later phase, so this is the
/// deliberate, temporary "publish" mechanism (see
/// Docs/specs/payments/payment-processing.md). <see cref="PaymentsDbContext"/>
/// and <see cref="IDomainEventDispatcher"/> are both Scoped, so — unlike
/// everything else in this codebase, which resolves its dependencies
/// through normal constructor injection — this hosted service (registered
/// once for the app's lifetime) creates a new DI scope per poll via
/// <see cref="IServiceScopeFactory"/>, the standard pattern for a
/// singleton needing Scoped dependencies.
/// </summary>
public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;

    public OutboxPublisherBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await PublishPendingMessagesAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var pending = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            var eventType = IntegrationEventTypeRegistry.Resolve(message.Type);
            var integrationEvent = (IntegrationEvent)JsonSerializer.Deserialize(message.PayloadJson, eventType)!;

            await dispatcher.DispatchAsync([(IDomainEvent)integrationEvent], cancellationToken);

            message.ProcessedAt = DateTimeOffset.UtcNow;
        }

        if (pending.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
