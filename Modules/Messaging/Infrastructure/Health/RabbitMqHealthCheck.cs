using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Health;

/// <summary>
/// The API's connection to RabbitMQ is open. While the client is recovering
/// it after a network failure it is not, and the API is not ready: nothing
/// it publishes or consumes moves until the connection is back (orders are
/// still accepted — their events wait in the outboxes).
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnection _connection;

    public RabbitMqHealthCheck(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connection.GetAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The RabbitMQ connection is down and recovering.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ can't be reached.", exception);
        }
    }
}
