using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// TestServer has no network peer; this sets the connection's remote
/// address from a test-only header, before the app's own middleware (the
/// forwarded headers and the rate limiter included) runs — standing in for
/// "who connected". Register with
/// <c>services.AddTransient&lt;IStartupFilter, RemoteAddressFromTestHeader&gt;()</c>.
/// </summary>
internal sealed class RemoteAddressFromTestHeader : IStartupFilter
{
    public const string Header = "X-Test-Remote-Address";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(Header, out var address))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
            }

            await nextMiddleware();
        });
        next(app);
    };
}
