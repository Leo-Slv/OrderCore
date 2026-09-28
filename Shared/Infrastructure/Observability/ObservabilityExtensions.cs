using System.Reflection;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderCore.Api.Shared.Infrastructure.Observability;

/// <summary>
/// Traces, metrics and logs with OpenTelemetry (Docs/specs/observability/observability.md).
/// <list type="bullet">
/// <item>Traces: ASP.NET Core, <c>HttpClient</c> and Npgsql (SQL text, never
/// parameter values), plus every <c>OrderCore.*</c> activity source the
/// modules and messaging open; parent-based sampling, everything by default.</item>
/// <item>Metrics: ASP.NET Core, <c>HttpClient</c>, runtime, Npgsql and every
/// <c>OrderCore.*</c> meter.</item>
/// <item>Logs: through the OpenTelemetry provider too, so each line carries its
/// trace and span; on the console as plain text in Development and JSON
/// elsewhere.</item>
/// </list>
/// Everything is exported over OTLP to <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
/// (Grafana LGTM in <c>docker compose</c>). With no endpoint — tests, a bare
/// <c>dotnet run</c> — nothing is exported and nothing fails.
/// </summary>
public static class ObservabilityExtensions
{
    public const string ServiceName = "ordercore-api";

    /// <summary>The prefix of every activity source and meter the application itself opens.</summary>
    public const string OrderCoreInstrumentation = "OrderCore.*";

    public const string OtlpEndpointSetting = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public static IHostApplicationBuilder AddOrderCoreObservability(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);
        }
        else
        {
            builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        }

        var samplingRatio = builder.Configuration.GetValue("Observability:TraceSamplingRatio", 1.0);
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: version)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                .AddAspNetCoreInstrumentation(options => options.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddSource(OrderCoreInstrumentation))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddMeter(OrderCoreInstrumentation));

        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointSetting]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
