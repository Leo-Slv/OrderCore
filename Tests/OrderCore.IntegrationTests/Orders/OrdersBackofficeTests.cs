using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The Orders backoffice endpoints through the real host, database and
/// outbox: an admin finds a confirmed order, reads it in full, moves it
/// through fulfilment (capturing the payment on shipping), and sees it on
/// the dashboard.
/// </summary>
public sealed class OrdersBackofficeTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public OrdersBackofficeTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_admin_fulfils_a_confirmed_order_and_the_payment_is_captured_when_it_ships()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (customer, tokens) = await SignUpCustomerAsync(factory, name: "Fulfilment Buyer");
        var customerId = tokens.GetProperty("customerId").GetGuid();
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Shipped Speaker", 250m);
        await _database.SeedStockAsync(product.Id, quantity: 3);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 1, idempotencyKey: "fulfilment-1");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");

        // The admin list shows the buyer and the payment.
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders?customerId={customerId}&status=Confirmed", Json);
        var row = list.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        row.GetProperty("customer").GetProperty("name").GetString().Should().Be("Fulfilment Buyer");
        row.GetProperty("paymentStatus").GetString().Should().Be("Authorized");

        // Notes, then the full detail: notes, customer, payment and the consumed reservation.
        (await admin.PutAsJsonAsync($"/api/orders/{orderId}/internal-notes", new { notes = "gift wrap" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}", Json);
        detail.GetProperty("internalNotes").GetString().Should().Be("gift wrap");
        detail.GetProperty("order").GetProperty("status").GetString().Should().Be("Confirmed");
        detail.GetProperty("customer").GetProperty("id").GetGuid().Should().Be(customerId);
        detail.GetProperty("payment").GetProperty("providerReference").GetString().Should().NotBeNullOrEmpty();
        detail.GetProperty("reservations").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("Consumed");

        // Fulfilment.
        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var shipped = await admin.PostAsync($"/api/orders/{orderId}/ship", null);
        shipped.StatusCode.Should().Be(HttpStatusCode.OK);
        (await shipped.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("payment").GetProperty("status").GetString()
            .Should().Be("Captured");
        (await admin.PostAsync($"/api/orders/{orderId}/deliver", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/status-history", Json);
        history.EnumerateArray().Select(h => h.GetProperty("toStatus").GetString()).Should().EndWith(
            ["Confirmed", "Processing", "Shipped", "Delivered"]);

        // The customer never sees the internal notes.
        var asCustomer = await customer.GetStringAsync($"/api/orders/{orderId}");
        asCustomer.Should().NotContain("gift wrap");

        // Dashboard.
        var dashboard = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard", Json);
        dashboard.GetProperty("ordersByStatus").GetProperty("Delivered").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        dashboard.GetProperty("revenueByCurrency").GetProperty("BRL").GetDecimal().Should().BeGreaterThanOrEqualTo(250m);
        dashboard.GetProperty("recentOrders").EnumerateArray().Should().Contain(o => o.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Fulfilment_steps_out_of_order_are_refused()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Impatient Kettle", 80m);
        await _database.SeedStockAsync(product.Id, quantity: 1);
        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 1, idempotencyKey: "out-of-order-1");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");

        var shipTooEarly = await admin.PostAsync($"/api/orders/{orderId}/ship", null);

        shipTooEarly.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await shipTooEarly.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("invalid_order_state");
        var payment = await admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json);
        payment.GetProperty("status").GetString().Should().Be("Authorized");
    }
}
