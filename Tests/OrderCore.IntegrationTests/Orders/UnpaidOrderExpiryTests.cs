using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// An order whose payment never started stops holding stock
/// (Docs/specs/orders/unpaid-order-expiry.md), through the real host, job
/// and database, with a 2-second window: the payment provider times out
/// during checkout, so the order is saved and its stock reserved but no
/// payment exists; after the window the order is <c>PaymentFailed</c> with
/// <c>payment_not_started</c> and the units are available again.
/// </summary>
public sealed class UnpaidOrderExpiryTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public UnpaidOrderExpiryTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_order_whose_payment_never_started_ends_and_frees_its_stock()
    {
        await using var factory = _database.CreateFactory(FakePaymentProviderMode.Timeout).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Orders:UnpaidOrderExpiry:Window", "00:00:02");
            builder.UseSetting("Orders:UnpaidOrderExpiry:CheckInterval", "00:00:00.500");
        });
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Unpaid Kettle", 80m);
        await _database.SeedStockAsync(product.Id, 3);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, 2, $"unpaid-{Guid.NewGuid():N}");
        checkout.IsSuccessStatusCode.Should().BeFalse("the payment provider timed out after the order was saved");

        var mine = await customer.GetFromJsonAsync<JsonElement>("/api/orders/me", Json);
        var orderId = mine.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
        var stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityAvailable").GetInt32().Should().Be(1, "2 units are reserved for the unpaid order");

        await PollOrderUntilAsync(customer, orderId, status => status == "PaymentFailed");

        var history = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/status-history", Json);
        history.EnumerateArray().Last().GetProperty("reason").GetString().Should().Be("payment_not_started");
        stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityAvailable").GetInt32().Should().Be(3);
        (await admin.GetAsync($"/api/payments/orders/{orderId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
