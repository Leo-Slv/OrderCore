using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Shared.Presentation.ExceptionHandling;

namespace OrderCore.Api.Shared.Presentation.Observability;

/// <summary>
/// Hands the request's trace back to the caller, so a support request or a
/// failing call in the browser can be looked up in the traces and logs:
/// every response carries the W3C <c>traceparent</c> header, and every
/// <see cref="ProblemDetails"/> a <c>traceId</c> (the 32-hex trace id
/// Grafana searches by).
/// </summary>
public static class TraceResponseExtensions
{
    public const string TraceParentHeader = "traceparent";

    public const string TraceIdExtension = "traceId";

    /// <summary>Adds <c>traceparent</c> to every response, before its headers are sent.</summary>
    public static IApplicationBuilder UseTraceResponseHeader(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (Activity.Current is { } activity && activity.IdFormat == ActivityIdFormat.W3C)
                {
                    context.Response.Headers[TraceParentHeader] = activity.Id;
                }

                return Task.CompletedTask;
            });

            return next(context);
        });

    /// <summary>
    /// <see cref="ProblemDetailsOptions.CustomizeProblemDetails"/> step: the
    /// code (<see cref="ProblemDetailsDefaults"/>) and the trace id.
    /// </summary>
    public static void Customize(ProblemDetailsContext context)
    {
        ProblemDetailsDefaults.AddDefaultCode(context);

        if (Activity.Current is { } activity)
        {
            context.ProblemDetails.Extensions[TraceIdExtension] = activity.TraceId.ToHexString();
        }
    }
}
