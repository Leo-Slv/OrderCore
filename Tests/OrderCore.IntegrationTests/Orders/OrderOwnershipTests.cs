using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Two real customers and an admin against one real order: the order's own
/// customer and the admin can read it, the other customer can't even tell
/// it exists.
/// </summary>
public sealed class OrderOwnershipTests : IClassFixture<ApiDatabase>, IAsyncLifetime
{
    private readonly ApiDatabase _database;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _owner = null!;
    private HttpClient _otherCustomer = null!;
    private HttpClient _admin = null!;
    private Guid _orderId;

    public OrderOwnershipTests(ApiDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        _factory = _database.CreateFactory();
        _admin = await SignInAsAdminAsync(_factory);
        (_owner, _) = await SignUpCustomerAsync(_factory, "Owner");
        (_otherCustomer, _) = await SignUpCustomerAsync(_factory, "Someone Else");

        var product = await CreatePublishedProductAsync(_admin, $"Product {Guid.NewGuid():N}", price: 50m);
        await _database.SeedStockAsync(product.Id, quantity: 10);
        var addressId = await AddAddressAsync(_owner);

        var checkout = await CheckoutAsync(_owner, addressId, product.Id, quantity: 1, idempotencyKey: $"ownership-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task The_owner_and_the_admin_can_read_the_order()
    {
        (await _owner.GetAsync($"/api/orders/{_orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _owner.GetAsync($"/api/orders/{_orderId}/status-history")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.GetAsync($"/api/orders/{_orderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _admin.GetAsync($"/api/orders/{_orderId}/status-history")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Another_customer_gets_the_same_404_as_for_an_order_that_does_not_exist()
    {
        var someoneElses = await _otherCustomer.GetAsync($"/api/orders/{_orderId}");
        var history = await _otherCustomer.GetAsync($"/api/orders/{_orderId}/status-history");
        var nonexistent = await _otherCustomer.GetAsync($"/api/orders/{Guid.NewGuid()}");

        someoneElses.StatusCode.Should().Be(HttpStatusCode.NotFound);
        history.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodeOfAsync(someoneElses)).Should().Be(await CodeOfAsync(nonexistent)).And.Be("order_not_found");
    }

    [Fact]
    public async Task Each_customer_only_sees_their_own_orders_in_their_history()
    {
        var ownerOrders = await _owner.GetFromJsonAsync<JsonElement>("/api/orders/me", Json);
        var otherOrders = await _otherCustomer.GetFromJsonAsync<JsonElement>("/api/orders/me", Json);

        ownerOrders.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).Should().Contain(_orderId);
        otherOrders.GetProperty("totalItems").GetInt32().Should().Be(0);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString();
}
