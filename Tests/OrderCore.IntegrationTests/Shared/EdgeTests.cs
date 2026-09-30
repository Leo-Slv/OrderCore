using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// The API's edge (production-readiness spec, item 3): forwarded headers are
/// honoured only from trusted proxies, HTTPS is enforced outside development
/// (but never on the health checks), and every response carries the security
/// headers, with <c>no-store</c> on authenticated ones.
/// </summary>
public sealed class EdgeTests : IClassFixture<OrderCoreApiFactory>
{
    private const string TrustedProxy = "10.0.0.5";
    private const string Stranger = "203.0.113.9";

    private readonly OrderCoreApiFactory _factory;

    public EdgeTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A deployed-like host: Production, HTTPS on 443, one trusted proxy.</summary>
    private WebApplicationFactory<Program> Production(bool trustAllProxies = false) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");

            // The settings a deployment must provide (the production settings check).
            builder.UseSetting("ConnectionStrings:OrderCoreDb", "Host=db.internal;Database=ordercore;Username=ordercore;Password=unused");
            builder.UseSetting("Cors:AllowedOrigins:0", "https://shop.example");
            builder.UseSetting("AllowedHosts", "api.shop.example");
            builder.UseSetting("Notifications:From", "OrderCore <no-reply@shop.example>");
            builder.UseSetting("Notifications:Smtp:Host", "smtp.mail.example");
            builder.UseSetting("https_port", "443");
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", TrustedProxy);
            builder.UseSetting("ForwardedHeaders:TrustAllProxies", trustAllProxies.ToString());
            builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, RemoteAddressFromTestHeader>());
        });

    private static HttpRequestMessage Request(string path, string from, string? forwardedProto = null)
    {
        // Not localhost: ASP.NET never sends HSTS to localhost, so a developer's
        // browser doesn't get stuck on HTTPS.
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "api.shop.example";
        request.Headers.Add(RemoteAddressFromTestHeader.Header, from);
        if (forwardedProto is not null)
        {
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
            request.Headers.Add("X-Forwarded-For", "198.51.100.20");
        }

        return request;
    }

    private static HttpClient NoRedirects(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Https_from_a_trusted_proxy_is_served_with_HSTS()
    {
        using var client = NoRedirects(Production());

        var response = await client.SendAsync(Request("/", from: TrustedProxy, forwardedProto: "https"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Strict-Transport-Security").Single().Should().Contain("max-age=31536000").And.Contain("includeSubDomains");
    }

    [Fact]
    public async Task Forwarded_headers_from_anyone_else_are_ignored_so_plain_http_is_redirected()
    {
        using var client = NoRedirects(Production());

        var response = await client.SendAsync(Request("/", from: Stranger, forwardedProto: "https"));

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location!.Scheme.Should().Be("https");
        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Trusting_all_proxies_honours_any_caller()
    {
        using var client = NoRedirects(Production(trustAllProxies: true));

        var response = await client.SendAsync(Request("/", from: Stranger, forwardedProto: "https"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_checks_answer_over_plain_http()
    {
        using var client = NoRedirects(Production());

        var response = await client.SendAsync(Request("/health/live", from: Stranger));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Development_neither_redirects_nor_sends_HSTS()
    {
        using var client = NoRedirects(_factory);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task Every_response_carries_the_security_headers()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[] { "/", "/api/no-such-route" })
        {
            var response = await client.GetAsync(path);

            response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff", path);
            response.Headers.GetValues("Referrer-Policy").Single().Should().Be("no-referrer", path);
            response.Headers.GetValues("X-Frame-Options").Single().Should().Be("DENY", path);
            response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'", path);
            response.Headers.CacheControl?.NoStore.Should().NotBe(true, "an anonymous response may be cached");
        }
    }

    [Fact]
    public async Task Answers_to_a_signed_in_caller_are_never_stored()
    {
        using var customer = _factory.CreateCustomerClient();

        var response = await customer.GetAsync("/api/no-such-route");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
