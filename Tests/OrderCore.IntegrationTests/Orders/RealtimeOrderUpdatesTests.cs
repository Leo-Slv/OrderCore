using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Real-time order updates end to end — HTTP, RabbitMQ and SignalR over the
/// test host: a customer follows their own order from checkout to shipping,
/// nobody else's; an admin follows every order.
/// </summary>
public sealed class RealtimeOrderUpdatesTests : IClassFixture<ApiDatabase>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly ApiDatabase _database;

    public RealtimeOrderUpdatesTests(ApiDatabase database)
    {
        _database = database;
    }

    /// <summary>A hub connection and every update it has received so far.</summary>
    private sealed class Screen : IAsyncDisposable
    {
        private readonly List<JsonElement> _updates = [];

        private Screen(HubConnection connection)
        {
            Connection = connection;
            connection.On<JsonElement>("orderUpdated", update =>
            {
                lock (_updates)
                {
                    _updates.Add(update);
                }
            });
        }

        public HubConnection Connection { get; }

        public static async Task<Screen> OpenAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string accessToken) =>
            new(await factory.ConnectToOrderUpdatesAsync(accessToken));

        public List<JsonElement> For(Guid orderId)
        {
            lock (_updates)
            {
                return _updates.Where(u => u.GetProperty("orderId").GetGuid() == orderId).ToList();
            }
        }

        public List<string> StatusesOf(Guid orderId) => For(orderId).Select(u => u.GetProperty("status").GetString()!).ToList();

        public async Task<List<string>> WaitForAsync(Guid orderId, string status)
        {
            var deadline = DateTimeOffset.UtcNow + Timeout;
            while (!StatusesOf(orderId).Contains(status))
            {
                if (DateTimeOffset.UtcNow > deadline)
                {
                    throw new TimeoutException($"No '{status}' update for order {orderId}; got {string.Join(", ", StatusesOf(orderId))}.");
                }

                await Task.Delay(100);
            }

            return StatusesOf(orderId);
        }

        public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
    }

    private async Task<(HttpClient Admin, Guid ProductId)> StoreAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Live Lantern", 55m);
        await _database.SeedStockAsync(product.Id, quantity: 10);
        return (admin, product.Id);
    }

    private static async Task<(HttpClient Client, string AccessToken, Guid AddressId)> CustomerAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var (client, tokens) = await SignUpCustomerAsync(factory);
        return (client, tokens.GetProperty("accessToken").GetString()!, await AddAddressAsync(client));
    }

    private static async Task<Guid> CheckOutAsync(HttpClient customer, Guid addressId, Guid productId)
    {
        var checkout = await CheckoutAsync(customer, addressId, productId, quantity: 1, $"live-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_customer_follows_their_order_live_and_nobody_else_sees_it()
    {
        await using var factory = _database.CreateFactory();
        var (_, productId) = await StoreAsync(factory);
        var buyer = await CustomerAsync(factory);
        var bystander = await CustomerAsync(factory);
        await using var buyerScreen = await Screen.OpenAsync(factory, buyer.AccessToken);
        await using var bystanderScreen = await Screen.OpenAsync(factory, bystander.AccessToken);

        var orderId = await CheckOutAsync(buyer.Client, buyer.AddressId, productId);

        var statuses = await buyerScreen.WaitForAsync(orderId, "Confirmed");
        statuses.Should().ContainInOrder("PendingPayment", "Confirmed");
        buyerScreen.For(orderId).Select(u => u.GetProperty("changedAt").GetDateTimeOffset()).Should().BeInAscendingOrder();
        var confirmed = buyerScreen.For(orderId).Last();
        confirmed.GetProperty("totalAmount").GetDecimal().Should().Be(55m);
        confirmed.GetProperty("currency").GetString().Should().Be("BRL");
        confirmed.GetProperty("orderNumber").GetString().Should().NotBeNullOrEmpty();

        await Task.Delay(500);
        bystanderScreen.For(orderId).Should().BeEmpty("a customer only ever gets their own orders");
    }

    [Fact]
    public async Task An_admin_sees_every_order_from_its_first_update()
    {
        await using var factory = _database.CreateFactory();
        var (_, productId) = await StoreAsync(factory);
        await using var backoffice = await Screen.OpenAsync(factory, factory.AdminToken());
        var first = await CustomerAsync(factory);
        var second = await CustomerAsync(factory);

        var firstOrder = await CheckOutAsync(first.Client, first.AddressId, productId);
        var secondOrder = await CheckOutAsync(second.Client, second.AddressId, productId);

        (await backoffice.WaitForAsync(firstOrder, "Confirmed")).First().Should().Be("Created", "a new order shows up as soon as it is placed");
        await backoffice.WaitForAsync(secondOrder, "Confirmed");
    }

    [Fact]
    public async Task Shipping_with_tracking_pushes_the_carrier_and_code()
    {
        await using var factory = _database.CreateFactory();
        var (admin, productId) = await StoreAsync(factory);
        var buyer = await CustomerAsync(factory);
        await using var buyerScreen = await Screen.OpenAsync(factory, buyer.AccessToken);
        var orderId = await CheckOutAsync(buyer.Client, buyer.AddressId, productId);
        await buyerScreen.WaitForAsync(orderId, "Confirmed");

        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/orders/{orderId}/ship", new { carrier = "Correios", trackingCode = "AB123456789BR" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await buyerScreen.WaitForAsync(orderId, "Shipped");
        var shipped = buyerScreen.For(orderId).Single(u => u.GetProperty("status").GetString() == "Shipped");
        shipped.GetProperty("shipment").GetProperty("carrier").GetString().Should().Be("Correios");
        shipped.GetProperty("shipment").GetProperty("trackingCode").GetString().Should().Be("AB123456789BR");
        buyerScreen.StatusesOf(orderId).Should().ContainInOrder("Confirmed", "Processing", "Shipped");
    }
}
