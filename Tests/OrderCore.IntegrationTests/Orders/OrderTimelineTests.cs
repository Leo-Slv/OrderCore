using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;
using OrderEvents = OrderCore.Api.Modules.Orders.Contracts.IntegrationEvents;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The admin order timeline through the real host and broker: an order's
/// whole life, from checkout to delivery, filed from three modules' events,
/// in the order it happened and once — even when the broker delivers an
/// event again.
/// </summary>
public sealed class OrderTimelineTests : IClassFixture<ApiDatabase>
{
    private static readonly string[] CheckoutToDelivery =
    [
        "orders.order-created",
        "inventory.stock-reserved",
        "orders.order-payment-requested",
        "payments.payment-authorized",
        "inventory.stock-consumed",
        "orders.order-confirmed",
        "orders.order-processing-started",
        "payments.payment-captured",
        "orders.order-shipped",
        "orders.order-delivered",
    ];

    private readonly ApiDatabase _database;

    public OrderTimelineTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_order_delivered_after_checkout_has_its_whole_life_in_the_timeline_once_and_in_order()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Timeline Lamp", 80m);
        await _database.SeedStockAsync(product.Id, quantity: 5);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 2, idempotencyKey: $"timeline-{Guid.NewGuid():N}");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");
        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/orders/{orderId}/ship", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/orders/{orderId}/deliver", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var timeline = await TimelineUntilAsync(admin, orderId, entries => entries.Count >= CheckoutToDelivery.Length);

        var types = timeline.Select(e => e.GetProperty("type").GetString()).ToList();
        types.Should().BeEquivalentTo(CheckoutToDelivery, "each event is filed once");
        timeline.Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).Should().BeInAscendingOrder();
        types.IndexOf("orders.order-created").Should().BeLessThan(types.IndexOf("orders.order-confirmed"));
        types.IndexOf("payments.payment-authorized").Should().BeLessThan(types.IndexOf("orders.order-confirmed"));
        types.IndexOf("orders.order-confirmed").Should().BeLessThan(types.IndexOf("orders.order-shipped"));
        types.IndexOf("payments.payment-captured").Should().BeLessThan(types.IndexOf("orders.order-shipped"));
        types.IndexOf("orders.order-shipped").Should().BeLessThan(types.IndexOf("orders.order-delivered"));

        var authorized = timeline.Single(e => e.GetProperty("type").GetString() == "payments.payment-authorized");
        authorized.GetProperty("source").GetString().Should().Be("payments");
        decimal.Parse(authorized.GetProperty("details").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(160m);
        authorized.GetProperty("details").GetProperty("currency").GetString().Should().Be("BRL");
        var reserved = timeline.Single(e => e.GetProperty("type").GetString() == "inventory.stock-reserved");
        reserved.GetProperty("details").GetProperty("productId").GetGuid().Should().Be(product.Id);
        reserved.GetProperty("details").GetProperty("quantity").GetString().Should().Be("2");
    }

    [Fact]
    public async Task An_event_delivered_again_is_not_filed_twice()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Twice Told Kettle", 40m);
        await _database.SeedStockAsync(product.Id, quantity: 3);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 1, idempotencyKey: $"timeline-{Guid.NewGuid():N}");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");
        var authorized = await _database.WaitForPublishedEventAsync<PaymentsDbContext>(PaymentAuthorized.Name, orderId);
        var created = await _database.WaitForPublishedEventAsync<OrdersDbContext>(OrderEvents.OrderCreated.Name, orderId);
        var before = await TimelineUntilAsync(admin, orderId, entries => entries.Any(e => e.GetProperty("type").GetString() == "orders.order-confirmed"));

        // The broker delivers the same two events again (at-least-once delivery).
        await TestBroker.RedeliverAsync(factory, authorized);
        await TestBroker.RedeliverAsync(factory, created);
        await Task.Delay(TimeSpan.FromSeconds(1));

        var after = await TimelineAsync(admin, orderId);
        after.Should().HaveCount(before.Count);
        after.Select(e => e.GetProperty("eventId").GetGuid()).Should().OnlyHaveUniqueItems();
    }

    private static async Task<List<JsonElement>> TimelineAsync(HttpClient admin, Guid orderId) =>
        (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}/timeline", Json)).EnumerateArray().ToList();

    /// <summary>The timeline is filled asynchronously; waits until it satisfies <paramref name="isDone"/>.</summary>
    private static async Task<List<JsonElement>> TimelineUntilAsync(HttpClient admin, Guid orderId, Func<List<JsonElement>, bool> isDone)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var timeline = await TimelineAsync(admin, orderId);
            if (isDone(timeline))
            {
                return timeline;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"Timeline of order {orderId} is still: {string.Join(", ", timeline.Select(e => e.GetProperty("type").GetString()))}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }
}
