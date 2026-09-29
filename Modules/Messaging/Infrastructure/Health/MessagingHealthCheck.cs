using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Modules.Messaging.Infrastructure.Telemetry;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Health;

/// <summary>
/// How the pipeline is doing, for the admin's health details (never for
/// readiness): each outbox's backlog and the age of its oldest row, and the
/// failed messages waiting for an admin. <c>Degraded</c> when a message is
/// waiting in the failed list or an outbox row has waited longer than
/// <see cref="StaleBacklog"/> — both need someone to look.
/// </summary>
public sealed class MessagingHealthCheck : IHealthCheck
{
    public static readonly TimeSpan StaleBacklog = TimeSpan.FromMinutes(1);

    private readonly MessagingTelemetry _telemetry;
    private readonly IServiceScopeFactory _scopeFactory;

    public MessagingHealthCheck(MessagingTelemetry telemetry, IServiceScopeFactory scopeFactory)
    {
        _telemetry = telemetry;
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var failedMessages = scope.ServiceProvider.GetRequiredService<IFailedMessageRepository>();
        var (_, pendingFailed) = await failedMessages.ListAsync(FailedMessageStatus.Pending, 1, 1, cancellationToken);

        var data = new Dictionary<string, object> { ["failedMessagesPending"] = pendingFailed };
        var stale = false;
        foreach (var (module, backlog) in _telemetry.Backlog)
        {
            data[$"outbox.{module}.pending"] = backlog.Pending;
            data[$"outbox.{module}.oldestAgeSeconds"] = Math.Round(backlog.OldestAgeSeconds, 1);
            stale |= backlog.OldestAgeSeconds > StaleBacklog.TotalSeconds;
        }

        if (pendingFailed > 0 || stale)
        {
            var why = pendingFailed > 0
                ? $"{pendingFailed} failed message(s) wait for an admin (messaging/failed-messages)."
                : "An outbox has rows waiting for more than a minute.";
            return HealthCheckResult.Degraded(why, data: data);
        }

        return HealthCheckResult.Healthy(data: data);
    }
}
