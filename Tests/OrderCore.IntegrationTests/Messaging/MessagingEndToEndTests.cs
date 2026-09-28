using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Inventory.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Messaging.Infrastructure.RabbitMq;
using OrderCore.Api.Modules.Orders;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;
using OrderEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

namespace OrderCore.IntegrationTests.Messaging;

/// <summary>
/// The storefront's checkout through the real host, database and broker,
/// for what messaging promises across modules: one trace from the request
/// to the confirmation, a redelivered event that changes nothing, and
/// orders still accepted while the broker is briefly away.
/// </summary>
public sealed class MessagingEndToEndTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public MessagingEndToEndTests(ApiDatabase database)
    {
        _database = database;
    }

    /// <summary>A signed-up customer with an address, and a published product with stock.</summary>
    private async Task<(HttpClient Admin, HttpClient Customer, Guid AddressId, Guid ProductId)> StorefrontAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string productName)
    {
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, productName, 30m);
        await _database.SeedStockAsync(product.Id, quantity: 10);
        return (admin, customer, addressId, product.Id);
    }

    private static async Task<Guid> CheckOutAsync(HttpClient customer, Guid addressId, Guid productId, string? traceParent = null)
    {
        var checkout = await CheckoutAsync(customer, addressId, productId, quantity: 1, $"e2e-{Guid.NewGuid():N}", traceParent);
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task One_checkout_is_one_trace_from_the_request_down_to_the_confirmation()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, addressId, productId) = await StorefrontAsync(factory, "Traced Teapot");
        var traceId = ActivityTraceId.CreateRandom();
        var traceParent = $"00-{traceId}-{ActivitySpanId.CreateRandom()}-01";

        var orderId = await CheckOutAsync(customer, addressId, productId, traceParent);
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");

        // Written by the checkout request itself...
        var created = await _database.WaitForPublishedEventAsync<OrdersDbContext>(OrderEvents.OrderCreated.Name, orderId);
        var reserved = await _database.WaitForPublishedEventAsync<InventoryDbContext>(StockReserved.Name, orderId);
        var authorized = await _database.WaitForPublishedEventAsync<PaymentsDbContext>(PaymentAuthorized.Name, orderId);

        // ...and by Orders' consumer, handling the authorization it received through the broker.
        var confirmed = await _database.WaitForPublishedEventAsync<OrdersDbContext>(OrderEvents.OrderConfirmed.Name, orderId);
        var consumed = await _database.WaitForPublishedEventAsync<InventoryDbContext>(StockConsumed.Name, orderId);

        foreach (var row in new[] { created, reserved, authorized, confirmed, consumed })
        {
            row.TraceParent.Should().Contain(traceId.ToString(), $"{row.Type} belongs to the checkout's trace");
        }

        created.CausationId.Should().BeNull("the request, not a message, caused it");
        confirmed.CausationId.Should().Be(authorized.Id, "the payment authorization caused the confirmation");
    }

    [Fact]
    public async Task A_payment_authorization_delivered_again_confirms_the_order_once()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, addressId, productId) = await StorefrontAsync(factory, "Duplicate Drum");
        var orderId = await CheckOutAsync(customer, addressId, productId);
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");
        var authorized = await _database.WaitForPublishedEventAsync<PaymentsDbContext>(PaymentAuthorized.Name, orderId);

        await TestBroker.RedeliverAsync(factory, authorized);
        await TestBroker.RedeliverAsync(factory, authorized);
        await Task.Delay(TimeSpan.FromSeconds(1));

        var history = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/status-history", Json);
        history.EnumerateArray().Count(h => h.GetProperty("toStatus").GetString() == "Confirmed").Should().Be(1);
        (await _database.OrdersHandledAsync(authorized.Id, OrdersDependencyInjection.PaymentOutcomesQueue)).Should().BeTrue();
        var timeline = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}/timeline", Json);
        timeline.EnumerateArray().Count(e => e.GetProperty("type").GetString() == PaymentAuthorized.Name).Should().Be(1);
        (await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{productId}", Json))
            .GetProperty("quantityOnHand").GetInt32().Should().Be(9, "the stock was consumed once");
    }

    [Fact]
    public async Task Orders_are_accepted_while_the_broker_is_away_and_confirmed_once_it_is_back()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, addressId, productId) = await StorefrontAsync(factory, "Patient Parcel");
        var virtualHost = factory.Services.GetRequiredService<IOptions<RabbitMqOptions>>().Value.VirtualHost;

        (await TestBroker.CloseConnectionsAsync(virtualHost)).Should().BeGreaterThan(0);
        var orderId = await CheckOutAsync(customer, addressId, productId);

        // The checkout only wrote to the database; its events wait in the
        // outboxes until the connection recovers, then go out and the
        // consumers — subscribed again on the recovered connection — confirm it.
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");
        await _database.WaitForPublishedEventAsync<OrdersDbContext>(OrderEvents.OrderConfirmed.Name, orderId);
    }
}
