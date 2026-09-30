using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace OrderCore.Api.Shared.Presentation.Hosting;

/// <summary>Section <c>ForwardedHeaders</c>: which proxies in front of the API are trusted.</summary>
public sealed class TrustedProxiesOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Addresses of the proxies/load balancers in front of the API (e.g. <c>10.0.0.4</c>).</summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>Networks they live in, in CIDR notation (e.g. <c>10.0.0.0/24</c>).</summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>
    /// Trust the forwarded headers of any caller — only for platforms where
    /// the API can't be reached except through their own proxy and its
    /// addresses aren't fixed. Off by default: anyone who can reach the API
    /// directly could otherwise pick their own client address and scheme.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}

/// <summary>
/// Behind a TLS-terminating proxy or load balancer the API sees the proxy's
/// address and plain HTTP. <c>X-Forwarded-For</c>/<c>X-Forwarded-Proto</c>
/// restore the client's address (rate limits, logs) and the original scheme
/// (HSTS, HTTPS redirection, generated links) — but only when they come from
/// a trusted proxy (<see cref="TrustedProxiesOptions"/>); loopback is always
/// trusted, for a proxy on the same machine.
/// </summary>
public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddOrderCoreForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var trusted = configuration.GetSection(TrustedProxiesOptions.SectionName).Get<TrustedProxiesOptions>() ?? new TrustedProxiesOptions();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (trusted.TrustAllProxies)
            {
                // Empty lists mean "any caller".
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                return;
            }

            foreach (var proxy in trusted.KnownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in trusted.KnownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}
