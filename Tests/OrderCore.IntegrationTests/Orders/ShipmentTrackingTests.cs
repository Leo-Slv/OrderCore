using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;
using OrderEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Shipping with the carrier, tracking code and link: the customer sees them
/// on the order and they go out with the <c>orders.order-shipped</c> event;
/// details that don't hold up are refused before the payment is captured.
/// </summary>
public sealed class ShipmentTrackingTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public ShipmentTrackingTests(ApiDatabase database)
    {
        _database = database;
    }

    private async Task<(HttpClient Admin, HttpClient Customer, Guid OrderId)> OrderInProcessingAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Tracked Toaster", 60m);
        await _database.SeedStockAsync(product.Id, quantity: 2);
        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 1, $"tracked-{Guid.NewGuid():N}");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");
        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return (admin, customer, orderId);
    }

    [Fact]
    public async Task Shipping_with_tracking_shows_it_to_the_customer_and_announces_it()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, orderId) = await OrderInProcessingAsync(factory);

        var shipped = await admin.PostAsJsonAsync($"/api/orders/{orderId}/ship", new
        {
            carrier = "Correios",
            trackingCode = "AB123456789BR",
            trackingUrl = "https://rastreamento.correios.com.br/AB123456789BR",
        });

        shipped.StatusCode.Should().Be(HttpStatusCode.OK);
        var asCustomer = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
        asCustomer.GetProperty("status").GetString().Should().Be("Shipped");
        var shipment = asCustomer.GetProperty("shipment");
        shipment.GetProperty("carrier").GetString().Should().Be("Correios");
        shipment.GetProperty("trackingCode").GetString().Should().Be("AB123456789BR");
        shipment.GetProperty("trackingUrl").GetString().Should().Be("https://rastreamento.correios.com.br/AB123456789BR");

        var announced = MessageEnvelope.FromOutbox(
            await _database.WaitForPublishedEventAsync<OrdersDbContext>(OrderEvents.OrderShipped.Name, orderId)).Payload;
        announced.GetProperty("carrier").GetString().Should().Be("Correios");
        announced.GetProperty("trackingCode").GetString().Should().Be("AB123456789BR");
    }

    [Fact]
    public async Task Bad_tracking_is_refused_before_the_payment_is_captured()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, orderId) = await OrderInProcessingAsync(factory);

        var refused = await admin.PostAsJsonAsync($"/api/orders/{orderId}/ship", new { trackingCode = "AB123", trackingUrl = "not a link" });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("validation_error");
        var order = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
        order.GetProperty("status").GetString().Should().Be("Processing");
        order.GetProperty("payment").GetProperty("status").GetString().Should().Be("Authorized", "nothing was captured");
    }

    [Fact]
    public async Task Shipping_without_a_body_ships_without_tracking()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, orderId) = await OrderInProcessingAsync(factory);

        (await admin.PostAsync($"/api/orders/{orderId}/ship", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
        order.GetProperty("status").GetString().Should().Be("Shipped");
        order.GetProperty("shipment").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
