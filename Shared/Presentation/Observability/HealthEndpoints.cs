using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderCore.Api.Shared.Infrastructure.Observability;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Shared.Presentation.Observability;

/// <summary>
/// Health endpoints (Docs/specs/observability/observability.md):
/// <list type="bullet">
/// <item><c>GET /health/live</c> (and <c>/health</c>, its alias): the process
/// answers; no dependency is checked, so a slow database never gets a live
/// API restarted.</item>
/// <item><c>GET /health/ready</c>: the checks tagged <see cref="Ready"/> —
/// PostgreSQL and RabbitMQ — answered as just <c>Healthy</c>/<c>Unhealthy</c>
/// (200/503), anonymously.</item>
/// <item><c>GET /health/details</c> (admin): every check with its status,
/// duration, description and data — including the outbox backlog and the
/// failed messages waiting for an admin.</item>
/// </list>
/// Modules add their own checks with the tags below.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>A dependency the API can't serve requests without.</summary>
    public const string Ready = "ready";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IServiceCollection AddOrderCoreHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgresql", tags: [Ready], timeout: TimeSpan.FromSeconds(5));
        return services;
    }

    public static IEndpointRouteBuilder MapOrderCoreHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        var live = new HealthCheckOptions { Predicate = _ => false };
        endpoints.MapHealthChecks("/health", live).AllowAnonymous();
        endpoints.MapHealthChecks("/health/live", live).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(Ready) })
            .AllowAnonymous();

        endpoints.MapHealthChecks("/health/details", new HealthCheckOptions { ResponseWriter = WriteDetailsAsync })
            .RequireAuthorization(AuthorizationPolicies.Admin);

        return endpoints;
    }

    private static Task WriteDetailsAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            durationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description ?? entry.Value.Exception?.Message,
                data = entry.Value.Data,
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
