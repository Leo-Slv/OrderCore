using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Smoke test proving the API host boots end-to-end. Real Orders
/// integration tests (persistence round-trip, concurrency, outbox once
/// implemented) are added to this folder as the corresponding
/// Infrastructure pieces land — see the note in
/// OrderCore.UnitTests/Inventory/InventoryReservationTests.cs for why the
/// concurrency scenario (section 34) belongs here and not in unit tests.
/// </summary>
public sealed class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
