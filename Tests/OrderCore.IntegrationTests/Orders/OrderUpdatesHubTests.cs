using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Who may connect to the order updates hub: any signed-in user, never an
/// anonymous or expired one; and the token in the query string counts only
/// on hub paths.
/// </summary>
public sealed class OrderUpdatesHubTests : IClassFixture<OrderCoreApiFactory>
{
    private readonly OrderCoreApiFactory _factory;

    public OrderUpdatesHubTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task A_signed_in_customer_and_an_admin_connect()
    {
        await using var customer = await _factory.ConnectToOrderUpdatesAsync(_factory.CustomerToken(Guid.NewGuid()));
        await using var admin = await _factory.ConnectToOrderUpdatesAsync(_factory.AdminToken());

        customer.State.Should().Be(HubConnectionState.Connected);
        admin.State.Should().Be(HubConnectionState.Connected);
    }

    [Fact]
    public async Task An_anonymous_connection_is_refused()
    {
        var connect = () => _factory.ConnectToOrderUpdatesAsync(accessToken: null);

        (await connect.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        var expired = _factory.AdminToken(issuedAt: DateTimeOffset.UtcNow.AddHours(-2));

        var connect = () => _factory.ConnectToOrderUpdatesAsync(expired);

        (await connect.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_token_in_the_query_string_is_ignored_outside_the_hubs()
    {
        var token = _factory.CustomerToken(Guid.NewGuid());

        var response = await _factory.CreateClient().GetAsync($"/api/orders/me?access_token={token}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
