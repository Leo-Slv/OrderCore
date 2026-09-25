using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Backoffice flows that cross modules, through the real host, database,
/// outbox and (fake) payment provider: cancelling a paid order settles the
/// money and puts the stock back (backoffice decision 2), a refused capture
/// stops the shipment (decision 1), and a delivered order can still be
/// refunded. Each flow checks every module it touches, the way the admin
/// screens would show it.
/// </summary>
public sealed class BackofficeFlowTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public BackofficeFlowTests(ApiDatabase database)
    {
        _database = database;
    }

    /// <summary>A published product with <paramref name="stock"/> units and a customer's confirmed order for <paramref name="quantity"/> of them.</summary>
    private async Task<(HttpClient Admin, HttpClient Customer, Guid ProductId, Guid OrderId)> ConfirmedOrderAsync(
        WebApplicationFactory<Program> factory, string productName, int stock, int quantity)
    {
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, productName, 100m);
        await _database.SeedStockAsync(product.Id, stock);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity, idempotencyKey: $"flow-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        await PollOrderUntilAsync(admin, orderId, status => status == "Confirmed");

        return (admin, customer, product.Id, orderId);
    }

    private static async Task<int> OnHandAsync(HttpClient admin, Guid productId) =>
        (await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{productId}", Json)).GetProperty("quantityOnHand").GetInt32();

    private static async Task<string> PaymentStatusAsync(HttpClient admin, Guid orderId) =>
        (await admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json)).GetProperty("status").GetString()!;

    [Fact]
    public async Task Cancelling_a_confirmed_order_voids_the_payment_and_puts_the_stock_back()
    {
        await using var factory = _database.CreateFactory();
        var (admin, customer, productId, orderId) = await ConfirmedOrderAsync(factory, "Cancelled Clock", stock: 5, quantity: 2);
        (await OnHandAsync(admin, productId)).Should().Be(3, "confirming consumed the 2 reserved units");

        var cancel = await admin.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { reason = "customer asked by phone" });

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("paymentSettlement").GetString().Should().Be("Voided");

        // Payments: released, never captured.
        (await PaymentStatusAsync(admin, orderId)).Should().Be("Voided");

        // Inventory: the units are back, the reservation says so, and the history shows the return.
        (await OnHandAsync(admin, productId)).Should().Be(5);
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}", Json);
        detail.GetProperty("reservations").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("Returned");
        var movements = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{productId}/movements", Json);
        movements.GetProperty("items").EnumerateArray().Select(m => m.GetProperty("movementType").GetString())
            .Should().Contain("ReservationReturned");

        // Orders: the customer sees it cancelled, and the audit timeline names an actor for the cancellation.
        var asCustomer = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
        asCustomer.GetProperty("status").GetString().Should().Be("Cancelled");
        var timeline = await admin.GetFromJsonAsync<JsonElement>($"/api/audit-logs?entityName=Order&entityId={orderId}", Json);
        var cancelled = timeline.GetProperty("items").EnumerateArray().Single(e => e.GetProperty("action").GetString() == "OrderCancelled");
        cancelled.GetProperty("userId").ValueKind.Should().Be(JsonValueKind.String);
        cancelled.GetProperty("metadata").GetProperty("payment").GetString().Should().Be("Voided");

        // Repeating the cancellation is refused and changes nothing.
        var again = await admin.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { reason = "again" });
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await OnHandAsync(admin, productId)).Should().Be(5);
    }

    [Fact]
    public async Task A_refused_capture_stops_the_shipment_and_the_order_can_still_be_cancelled_without_charge()
    {
        await using var factory = _database.CreateFactory(FakePaymentProviderMode.CaptureDeclined);
        var (admin, _, productId, orderId) = await ConfirmedOrderAsync(factory, "Unshippable Chair", stock: 2, quantity: 1);
        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var ship = await admin.PostAsync($"/api/orders/{orderId}/ship", null);

        ship.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ship.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("payment_capture_failed");
        var stuck = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}", Json);
        stuck.GetProperty("order").GetProperty("status").GetString().Should().Be("Processing");
        stuck.GetProperty("payment").GetProperty("status").GetString().Should().Be("Authorized");

        var cancel = await admin.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { reason = "card could not be charged" });

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PaymentStatusAsync(admin, orderId)).Should().Be("Voided");
        (await OnHandAsync(admin, productId)).Should().Be(2);
    }

    [Fact]
    public async Task A_delivered_order_can_be_refunded_and_the_refund_shows_on_the_order()
    {
        await using var factory = _database.CreateFactory();
        var (admin, _, _, orderId) = await ConfirmedOrderAsync(factory, "Returned Toaster", stock: 1, quantity: 1);
        foreach (var step in new[] { "start-processing", "ship", "deliver" })
        {
            (await admin.PostAsync($"/api/orders/{orderId}/{step}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var paymentId = (await admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json)).GetProperty("id").GetGuid();
        var refund = await admin.PostAsJsonAsync($"/api/payments/{paymentId}/refunds", new { amount = 40m, reason = "arrived scratched" });
        refund.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/orders/{orderId}", Json);
        var payment = detail.GetProperty("payment");
        payment.GetProperty("status").GetString().Should().Be("Captured", "a partial refund leaves the rest captured");
        var refundRow = payment.GetProperty("refunds").EnumerateArray().Should().ContainSingle().Subject;
        refundRow.GetProperty("amount").GetDecimal().Should().Be(40m);
        refundRow.GetProperty("status").GetString().Should().Be("Completed");

        var cancel = await admin.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { reason = "too late" });
        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a delivered order is never cancelled");
    }
}
