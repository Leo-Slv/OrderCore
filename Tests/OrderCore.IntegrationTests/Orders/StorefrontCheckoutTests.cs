using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The storefront's whole path through the real HTTP host, against
/// PostgreSQL: catalog → cart quote → checkout → the outbox publisher
/// (running as the host's own background service) confirms the order →
/// tracking. Unlike <see cref="CheckoutFlowTests"/>, nothing is wired by
/// hand: routing, model binding, authentication, the exception handler,
/// DI and the background service are all the production ones. Only stock
/// is seeded directly, since no endpoint creates a stock record. Each test
/// gets its own database, since the catalog assertions expect it empty.
/// </summary>
public sealed class StorefrontCheckoutTests : IAsyncLifetime
{
    private readonly ApiDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Buyer_can_go_from_catalog_to_a_confirmed_order_and_follow_it()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (client, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(client);
        var product = await CreatePublishedProductAsync(admin, "Wireless Mouse", price: 150m);
        await _database.SeedStockAsync(product.Id, quantity: 5);

        // Catalog: the product card and page show it as purchasable.
        var listing = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?sort=PriceAsc", Json);
        listing.GetProperty("totalItems").GetInt32().Should().Be(1);
        listing.GetProperty("items")[0].GetProperty("availability").GetString().Should().Be("InStock");

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/catalog/products/by-slug/{product.Slug}", Json);
        page.GetProperty("id").GetGuid().Should().Be(product.Id);

        // Cart: a stale price is reported, the current one is accepted.
        var staleQuote = await PostJsonAsync(client, "/api/orders/cart/quote", new
        {
            items = new[] { new { productId = product.Id, quantity = 2, expectedUnitPrice = 140m } },
        });
        staleQuote.GetProperty("isValid").GetBoolean().Should().BeFalse();
        staleQuote.GetProperty("lines")[0].GetProperty("issue").GetString().Should().Be("PriceChanged");

        var quote = await PostJsonAsync(client, "/api/orders/cart/quote", new
        {
            items = new[] { new { productId = product.Id, quantity = 2, expectedUnitPrice = 150m } },
        });
        quote.GetProperty("isValid").GetBoolean().Should().BeTrue();
        quote.GetProperty("total").GetDecimal().Should().Be(300m);

        // Checkout: 202 with the order already awaiting payment.
        var checkout = await CheckoutAsync(client, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-e2e-1");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        checkout.Headers.Location.Should().NotBeNull();
        var created = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        var orderId = created.GetProperty("id").GetGuid();
        created.GetProperty("status").GetString().Should().Be("PendingPayment");
        created.GetProperty("totalAmount").GetDecimal().Should().Be(300m);
        created.GetProperty("shippingAddress").GetProperty("street").GetString().Should().Be("Rua das Flores");

        // Replaying the same checkout returns the same order.
        var replay = await CheckoutAsync(client, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-e2e-1");
        (await replay.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid().Should().Be(orderId);

        // Tracking: the outbox publisher confirms the order on its own.
        var confirmed = await PollOrderUntilAsync(client, orderId, status => status == "Confirmed");
        confirmed.GetProperty("confirmedAt").ValueKind.Should().Be(JsonValueKind.String);
        confirmed.GetProperty("payment").GetProperty("status").GetString().Should().Be("Authorized");
        confirmed.GetProperty("payment").GetProperty("method").GetString().Should().Be("Pix");

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/status-history", Json);
        history.EnumerateArray().Select(h => h.GetProperty("toStatus").GetString())
            .Should().Equal("Created", "PendingPayment", "Confirmed");

        var myOrders = await client.GetFromJsonAsync<JsonElement>("/api/orders/me", Json);
        myOrders.GetProperty("totalItems").GetInt32().Should().Be(1);
        myOrders.GetProperty("items")[0].GetProperty("itemCount").GetInt32().Should().Be(2);

        // The two units are gone from stock for good.
        var after = await client.GetFromJsonAsync<JsonElement>($"/api/catalog/products/by-slug/{product.Slug}", Json);
        after.GetProperty("availability").GetString().Should().Be("InStock");
        await using var inventoryDb = new InventoryDbContext(_database.Options<InventoryDbContext>());
        var stock = await new EfStockItemRepository(inventoryDb).GetByProductIdAsync(product.Id, CancellationToken.None);
        stock!.QuantityOnHand.Should().Be(3);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Declined_payment_ends_the_order_in_PaymentFailed_with_the_reason()
    {
        await using var factory = _database.CreateFactory(FakePaymentProviderMode.Declined);
        var admin = await SignInAsAdminAsync(factory);
        var (client, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(client);
        var product = await CreatePublishedProductAsync(admin, "Mechanical Keyboard", price: 400m);
        await _database.SeedStockAsync(product.Id, quantity: 1);

        var checkout = await CheckoutAsync(client, addressId, product.Id, quantity: 1, idempotencyKey: "checkout-declined");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var failed = await PollOrderUntilAsync(client, orderId, status => status == "PaymentFailed");
        failed.GetProperty("payment").GetProperty("status").GetString().Should().Be("Failed");
        failed.GetProperty("payment").GetProperty("failureReason").GetString().Should().Be("card_declined");
    }

    [Fact]
    public async Task Checkout_without_enough_stock_is_a_409_with_a_code_the_storefront_can_branch_on()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (client, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(client);
        var product = await CreatePublishedProductAsync(admin, "Monitor", price: 900m);
        await _database.SeedStockAsync(product.Id, quantity: 1);

        var checkout = await CheckoutAsync(client, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-no-stock");

        checkout.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().Should().Be("insufficient_stock");
    }

    [Fact]
    public async Task Checkout_without_an_idempotency_key_is_rejected()
    {
        await using var factory = _database.CreateFactory();
        var client = factory.CreateCustomerClient();

        var response = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } },
            shippingAddressId = Guid.NewGuid(),
            billingAddressId = Guid.NewGuid(),
            paymentMethod = "Pix",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

}
