using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace OrderCore.Api.Shared.Infrastructure.Observability;

/// <summary>
/// The database answers a trivial query. Every module shares the one
/// PostgreSQL database, so one check covers them all.
/// </summary>
public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly string? _connectionString;

    public PostgresHealthCheck(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("OrderCoreDb");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy("ConnectionStrings:OrderCoreDb is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL can't be reached.", exception);
        }
    }
}
