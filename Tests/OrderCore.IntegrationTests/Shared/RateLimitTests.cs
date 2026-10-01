using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// The rate limits of the anonymous and expensive endpoints
/// (production-readiness spec, decision 4), with a limit of 2 per window so
/// the third request is the one turned away: <c>429</c>, code
/// <c>too_many_requests</c>, a <c>Retry-After</c> — and only for the caller
/// who went over. None of these requests reach a database: a limit counts
/// every request, even one the endpoint then refuses.
/// </summary>
public sealed class RateLimitTests : IClassFixture<OrderCoreApiFactory>
{
    private const string FirstAddress = "198.51.100.10";
    private const string SecondAddress = "198.51.100.11";

    private readonly OrderCoreApiFactory _factory;

    public RateLimitTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A fresh host (fresh counters) where every policy allows 2 requests per hour.</summary>
    private WebApplicationFactory<Program> LowLimits() =>
        _factory.WithWebHostBuilder(builder =>
        {
            foreach (var policy in new[] { "SignIn", "SignUp", "Refresh", "ForgotPassword", "ResetPassword", "ConfirmEmail", "Checkout", "StripeWebhook" })
            {
                builder.UseSetting($"RateLimits:{policy}:PermitLimit", "2");
                builder.UseSetting($"RateLimits:{policy}:Window", "01:00:00");
            }

            builder.ConfigureTestServices(services => services.AddTransient<IStartupFilter, RemoteAddressFromTestHeader>());
        });

    private static HttpRequestMessage Post(string path, string from)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add(RemoteAddressFromTestHeader.Header, from);
        return request;
    }

    public static TheoryData<string> AnonymousEndpoints =>
    [
        "/api/auth/sign-in",
        "/api/auth/sign-up",
        "/api/auth/refresh",
        "/api/auth/password/forgot",
        "/api/auth/password/reset",
        "/api/auth/email/confirm",
        "/api/payments/webhooks/stripe",
    ];

    [Theory]
    [MemberData(nameof(AnonymousEndpoints))]
    public async Task An_address_over_the_limit_gets_429_and_another_address_does_not(string path)
    {
        await using var factory = LowLimits();
        using var client = factory.CreateClient();

        (await client.SendAsync(Post(path, FirstAddress))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        (await client.SendAsync(Post(path, FirstAddress))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        var third = await client.SendAsync(Post(path, FirstAddress));

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter!.Delta.Should().BePositive();
        var problem = await third.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("too_many_requests");
        problem.GetProperty("status").GetInt32().Should().Be(429);

        (await client.SendAsync(Post(path, SecondAddress))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Checkout_is_limited_per_customer_not_per_address()
    {
        await using var factory = LowLimits();
        using var first = factory.CreateCustomerClient();
        using var second = factory.CreateCustomerClient();

        for (var i = 0; i < 2; i++)
        {
            (await first.SendAsync(Post("/api/orders/checkout", FirstAddress))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        (await first.SendAsync(Post("/api/orders/checkout", FirstAddress))).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await second.SendAsync(Post("/api/orders/checkout", FirstAddress))).StatusCode
            .Should().NotBe(HttpStatusCode.TooManyRequests, "another customer from the same address has their own limit");
    }

    [Fact]
    public async Task Endpoints_without_a_policy_are_not_limited()
    {
        await using var factory = LowLimits();
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/api/payments/methods")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task The_OpenAPI_document_declares_429_on_the_limited_operations_only()
    {
        var document = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = document.GetProperty("paths");

        foreach (var (path, method) in new[]
        {
            ("/api/auth/sign-in", "post"), ("/api/auth/sign-up", "post"), ("/api/auth/refresh", "post"),
            ("/api/orders/checkout", "post"), ("/api/payments/webhooks/stripe", "post"),
        })
        {
            paths.GetProperty(path).GetProperty(method).GetProperty("responses").TryGetProperty("429", out _)
                .Should().BeTrue($"{method} {path} is rate limited");
        }

        paths.GetProperty("/api/payments/methods").GetProperty("get").GetProperty("responses").TryGetProperty("429", out _)
            .Should().BeFalse();
    }
}
