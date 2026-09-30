using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderCore.Api.Shared.Presentation.Authentication;

namespace OrderCore.Api.Shared.Presentation.RateLimiting;

/// <summary>A fixed-window limit, read from <c>RateLimits:&lt;Policy&gt;</c>.</summary>
public sealed class FixedWindowLimit
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }
}

/// <summary>
/// Throttling of the endpoints that are anonymous or expensive
/// (production-readiness spec, decision 4), on ASP.NET Core's built-in rate
/// limiter. Each module registers the policies of its own endpoints with
/// <see cref="AddFixedWindowPolicy"/> and applies them with
/// <c>[EnableRateLimiting(policy)]</c>; this class answers the rejections:
/// <c>429</c> as <c>ProblemDetails</c> with code <c>too_many_requests</c> and
/// a <c>Retry-After</c>, counted by policy and logged without the client's
/// address.
/// </summary>
public static class RateLimitingExtensions
{
    public const string SectionName = "RateLimits";

    public static IServiceCollection AddOrderCoreRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton<RateLimitingMetrics>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;
        });
        return services;
    }

    /// <summary>
    /// A fixed window per partition — the client's address (after the trusted
    /// forwarded headers) or, with <paramref name="perCustomer"/>, the signed-in
    /// customer — with the values of <c>RateLimits:{name}</c> or the defaults.
    /// </summary>
    public static IServiceCollection AddFixedWindowPolicy(
        this IServiceCollection services, IConfiguration configuration, string name, int permitLimit, TimeSpan window, bool perCustomer = false)
    {
        var limit = configuration.GetSection($"{SectionName}:{name}").Get<FixedWindowLimit>() ?? new FixedWindowLimit();
        if (limit.PermitLimit <= 0)
        {
            limit.PermitLimit = permitLimit;
        }

        if (limit.Window <= TimeSpan.Zero)
        {
            limit.Window = window;
        }

        services.AddRateLimiter(options => options.AddPolicy(name, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(context, perCustomer),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = limit.PermitLimit, Window = limit.Window, QueueLimit = 0 })));
        return services;
    }

    private static string PartitionKey(HttpContext context, bool perCustomer) =>
        perCustomer && context.User.FindFirst(OrderCoreClaimTypes.CustomerId)?.Value is { } customerId
            ? $"customer:{customerId}"
            : $"address:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static async ValueTask OnRejectedAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        var policy = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "unknown";
        context.RequestServices.GetRequiredService<RateLimitingMetrics>().Rejected(policy);
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OrderCore.RateLimiting")
            .LogWarning("Rate limit {Policy} exceeded.", policy);

        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "Too many requests; try again after the time in the Retry-After header.",
        };
        problem.Extensions["code"] = "too_many_requests";

        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }
}

/// <summary>Meter <c>OrderCore.Http</c>: requests turned away by a rate limit, by policy.</summary>
public sealed class RateLimitingMetrics
{
    public const string Name = "OrderCore.Http";

    private readonly Counter<long> _rejected;

    public RateLimitingMetrics(IMeterFactory meterFactory)
    {
        _rejected = meterFactory.Create(Name).CreateCounter<long>(
            "ordercore.http.rate_limited", "{request}", "Requests rejected by a rate limit, by policy.");
    }

    public void Rejected(string policy) => _rejected.Add(1, new KeyValuePair<string, object?>("ordercore.rate_limit_policy", policy));
}
