using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// Live answers whenever the process does; ready only while PostgreSQL and
/// RabbitMQ are reachable; the details are for admins and name what fails.
/// </summary>
public sealed class HealthEndpointsTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public HealthEndpointsTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task With_everything_up_the_api_is_live_and_ready()
    {
        await using var factory = _database.CreateFactory();
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Without_the_database_the_api_is_live_but_not_ready()
    {
        await using var factory = _database.CreateFactory().WithWebHostBuilder(builder => builder.UseSetting(
            "ConnectionStrings:OrderCoreDb", "Host=127.0.0.1;Port=1;Database=ordercore;Username=ordercore;Password=ordercore;Timeout=2"));
        var client = factory.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await ready.Content.ReadAsStringAsync()).Should().Be("Unhealthy", "readiness says nothing more to anonymous callers");
    }

    [Fact]
    public async Task Without_the_broker_the_api_is_not_ready_and_the_details_name_it()
    {
        await using var factory = _database.CreateFactory();
        var client = factory.CreateClient();
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        await TestBroker.DeleteVirtualHostAsync(factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value.VirtualHost);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        HttpStatusCode status;
        do
        {
            await Task.Delay(200);
            status = (await client.GetAsync("/health/ready")).StatusCode;
        }
        while (status != HttpStatusCode.ServiceUnavailable && DateTimeOffset.UtcNow < deadline);

        status.Should().Be(HttpStatusCode.ServiceUnavailable);
        var response = await (await SignInAsAdminAsync(factory)).GetAsync("/health/details");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "the details answer with the overall status too");
        var details = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var checks = details.GetProperty("checks").EnumerateArray().ToDictionary(c => c.GetProperty("name").GetString()!);
        checks["rabbitmq"].GetProperty("status").GetString().Should().Be("Unhealthy");
        checks["postgresql"].GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task The_details_are_for_admins_and_show_each_check_and_the_messaging_backlog()
    {
        await using var factory = _database.CreateFactory();

        (await factory.CreateClient().GetAsync("/health/details")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await factory.CreateCustomerClient().GetAsync("/health/details")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var details = await (await SignInAsAdminAsync(factory)).GetFromJsonAsync<JsonElement>("/health/details", Json);
        var checks = details.GetProperty("checks").EnumerateArray().ToDictionary(c => c.GetProperty("name").GetString()!);
        checks.Keys.Should().BeEquivalentTo(["postgresql", "rabbitmq", "messaging"]);
        checks["postgresql"].GetProperty("status").GetString().Should().Be("Healthy");
        checks["messaging"].GetProperty("data").GetProperty("failedMessagesPending").GetInt32().Should().Be(0);
        checks["messaging"].GetProperty("durationMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }
}
