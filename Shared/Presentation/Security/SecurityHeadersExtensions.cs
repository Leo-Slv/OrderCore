namespace OrderCore.Api.Shared.Presentation.Security;

/// <summary>
/// The usual security headers on every response of this JSON API:
/// <list type="bullet">
/// <item><c>X-Content-Type-Options: nosniff</c>, <c>Referrer-Policy: no-referrer</c>
/// and <c>X-Frame-Options: DENY</c>;</item>
/// <item>a <c>Content-Security-Policy</c> that lets nothing load or frame the
/// responses — except on the Development-only Scalar UI, which is a page that
/// needs its scripts;</item>
/// <item><c>Cache-Control: no-store</c> on every answer to an authenticated
/// request, so a customer's or admin's data is never kept by a browser or a
/// shared proxy cache.</item>
/// </list>
/// Written just before the response starts, so every status (errors, 404s,
/// the authorization middleware's 401/403) gets them.
/// </summary>
public static class SecurityHeadersExtensions
{
    private const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.XFrameOptions = "DENY";

                if (!context.Request.Path.StartsWithSegments("/scalar"))
                {
                    headers.ContentSecurityPolicy = ContentSecurityPolicy;
                }

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            await next();
        });
}
