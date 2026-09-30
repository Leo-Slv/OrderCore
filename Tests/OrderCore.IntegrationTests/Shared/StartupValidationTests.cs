using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// Misconfiguration fails fast (production-readiness spec, item 5): the host
/// refuses to start, with a message naming the setting, when Stripe is set up
/// in a way that would run but misbehave, or when a deployed environment is
/// missing a setting it must provide. The combinations are covered by the
/// validators' unit tests; these prove the host actually runs them.
/// </summary>
public sealed class StartupValidationTests : IClassFixture<OrderCoreApiFactory>
{
    private readonly OrderCoreApiFactory _factory;

    public StartupValidationTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    private static string Messages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
            {
                messages.AddRange(aggregate.InnerExceptions.Select(e => e.Message));
            }
        }

        return string.Join(" | ", messages);
    }

    private static Action Starting(WebApplicationFactory<Program> factory) => () => factory.CreateClient().Dispose();

    [Fact]
    public void Stripe_without_a_webhook_secret_stops_the_API()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payments:Stripe:SecretKey", "sk_test_ordercore");
            builder.UseSetting("Payments:Stripe:PublishableKey", "pk_test_ordercore");
        });

        Messages(Starting(factory).Should().Throw<Exception>().Which).Should().Contain("Payments:Stripe:WebhookSecret");
    }

    [Fact]
    public void A_live_Stripe_key_stops_the_API_unless_allowed()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payments:Stripe:SecretKey", "sk_live_ordercore");
            builder.UseSetting("Payments:Stripe:PublishableKey", "pk_live_ordercore");
            builder.UseSetting("Payments:Stripe:WebhookSecret", "whsec_ordercore");
        });

        Messages(Starting(factory).Should().Throw<Exception>().Which).Should().Contain("AllowLiveKeys");
    }

    [Fact]
    public async Task A_live_Stripe_key_starts_when_allowed()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payments:Stripe:SecretKey", "sk_live_ordercore");
            builder.UseSetting("Payments:Stripe:PublishableKey", "pk_live_ordercore");
            builder.UseSetting("Payments:Stripe:WebhookSecret", "whsec_ordercore");
            builder.UseSetting("Payments:Stripe:AllowLiveKeys", "true");
        });

        (await factory.CreateClient().GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void A_deployed_environment_without_its_settings_stops_the_API_naming_them()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var messages = Messages(Starting(factory).Should().Throw<Exception>().Which);

        messages.Should().Contain("ConnectionStrings:OrderCoreDb").And.Contain("Cors:AllowedOrigins").And.Contain("AllowedHosts");
    }
}
