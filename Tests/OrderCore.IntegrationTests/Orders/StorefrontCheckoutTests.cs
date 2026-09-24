using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// The storefront's whole path through the real HTTP host, against
/// PostgreSQL: catalog → cart quote → checkout → the outbox publisher
/// (running as the host's own background service) confirms the order →
/// tracking. Unlike <see cref="CheckoutFlowTests"/>, nothing is wired by
/// hand: routing, model binding, the exception handler, DI and the
/// background service are all the production ones. Only stock is seeded
/// directly, since no endpoint creates a stock record.
/// </summary>
public sealed class StorefrontCheckoutTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        await using (var db = new CustomersDbContext(Options<CustomersDbContext>(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new CatalogDbContext(Options<CatalogDbContext>(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new InventoryDbContext(Options<InventoryDbContext>(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new OrdersDbContext(Options<OrdersDbContext>(connectionString)))
        {
            await db.Database.MigrateAsync();
        }

        await using (var db = new PaymentsDbContext(Options<PaymentsDbContext>(connectionString)))
        {
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static DbContextOptions<T> Options<T>(string connectionString)
        where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(connectionString).Options;

    private WebApplicationFactory<Program> CreateFactory(FakePaymentProviderMode paymentMode = FakePaymentProviderMode.Success) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:OrderCoreDb", _postgres.GetConnectionString());
            builder.ConfigureTestServices(services =>
                services.Configure<FakePaymentProviderOptions>(options => options.Mode = paymentMode));
        });

    [Fact]
    public async Task Buyer_can_go_from_catalog_to_a_confirmed_order_and_follow_it()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var (customerId, addressId) = await CreateCustomerWithAddressAsync(client);
        var product = await CreatePublishedProductAsync(client, "Wireless Mouse", price: 150m);
        await SeedStockAsync(product.Id, quantity: 5);

        // Catalog: the product card and page show it as purchasable.
        var listing = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?active=true&sort=PriceAsc", Json);
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
        var checkout = await CheckoutAsync(client, customerId, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-e2e-1");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        checkout.Headers.Location.Should().NotBeNull();
        var created = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        var orderId = created.GetProperty("id").GetGuid();
        created.GetProperty("status").GetString().Should().Be("PendingPayment");
        created.GetProperty("totalAmount").GetDecimal().Should().Be(300m);
        created.GetProperty("shippingAddress").GetProperty("street").GetString().Should().Be("Rua das Flores");

        // Replaying the same checkout returns the same order.
        var replay = await CheckoutAsync(client, customerId, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-e2e-1");
        (await replay.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid().Should().Be(orderId);

        // Tracking: the outbox publisher confirms the order on its own.
        var confirmed = await PollOrderUntilAsync(client, orderId, status => status == "Confirmed");
        confirmed.GetProperty("confirmedAt").ValueKind.Should().Be(JsonValueKind.String);
        confirmed.GetProperty("payment").GetProperty("status").GetString().Should().Be("Authorized");
        confirmed.GetProperty("payment").GetProperty("method").GetString().Should().Be("Pix");

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}/status-history", Json);
        history.EnumerateArray().Select(h => h.GetProperty("toStatus").GetString())
            .Should().Equal("Created", "PendingPayment", "Confirmed");

        var myOrders = await client.GetFromJsonAsync<JsonElement>($"/api/orders/customers/{customerId}", Json);
        myOrders.GetProperty("totalItems").GetInt32().Should().Be(1);
        myOrders.GetProperty("items")[0].GetProperty("itemCount").GetInt32().Should().Be(2);

        // The two units are gone from stock for good.
        var after = await client.GetFromJsonAsync<JsonElement>($"/api/catalog/products/by-slug/{product.Slug}", Json);
        after.GetProperty("availability").GetString().Should().Be("InStock");
        await using var inventoryDb = new InventoryDbContext(Options<InventoryDbContext>(_postgres.GetConnectionString()));
        var stock = await new EfStockItemRepository(inventoryDb).GetByProductIdAsync(product.Id, CancellationToken.None);
        stock!.QuantityOnHand.Should().Be(3);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Declined_payment_ends_the_order_in_PaymentFailed_with_the_reason()
    {
        await using var factory = CreateFactory(FakePaymentProviderMode.Declined);
        var client = factory.CreateClient();
        var (customerId, addressId) = await CreateCustomerWithAddressAsync(client);
        var product = await CreatePublishedProductAsync(client, "Mechanical Keyboard", price: 400m);
        await SeedStockAsync(product.Id, quantity: 1);

        var checkout = await CheckoutAsync(client, customerId, addressId, product.Id, quantity: 1, idempotencyKey: "checkout-declined");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var failed = await PollOrderUntilAsync(client, orderId, status => status == "PaymentFailed");
        failed.GetProperty("payment").GetProperty("status").GetString().Should().Be("Failed");
        failed.GetProperty("payment").GetProperty("failureReason").GetString().Should().Be("card_declined");
    }

    [Fact]
    public async Task Checkout_without_enough_stock_is_a_409_with_a_code_the_storefront_can_branch_on()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        var (customerId, addressId) = await CreateCustomerWithAddressAsync(client);
        var product = await CreatePublishedProductAsync(client, "Monitor", price: 900m);
        await SeedStockAsync(product.Id, quantity: 1);

        var checkout = await CheckoutAsync(client, customerId, addressId, product.Id, quantity: 2, idempotencyKey: "checkout-no-stock");

        checkout.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        problem.GetProperty("code").GetString().Should().Be("insufficient_stock");
    }

    [Fact]
    public async Task Checkout_without_an_idempotency_key_is_rejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders/checkout", new
        {
            customerId = Guid.NewGuid(),
            items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } },
            shippingAddressId = Guid.NewGuid(),
            billingAddressId = Guid.NewGuid(),
            paymentMethod = "Pix",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<(Guid CustomerId, Guid AddressId)> CreateCustomerWithAddressAsync(HttpClient client)
    {
        var customerResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            name = "Jane Doe",
            email = $"jane-{Guid.NewGuid():N}@example.com",
            passwordHash = "hashed-password",
        });
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var addressResponse = await client.PostAsJsonAsync($"/api/customers/{customerId}/addresses", new
        {
            label = "Home",
            recipientName = "Jane Doe",
            street = "Rua das Flores",
            number = "42",
            neighborhood = "Centro",
            city = "São Paulo",
            state = "SP",
            postalCode = "01000-000",
            country = "BR",
        });
        addressResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var addresses = await client.GetFromJsonAsync<JsonElement>($"/api/customers/{customerId}/addresses", Json);
        return (customerId, addresses[0].GetProperty("id").GetGuid());
    }

    private static async Task<(Guid Id, string Slug)> CreatePublishedProductAsync(HttpClient client, string name, decimal price)
    {
        var categoryResponse = await client.PostAsJsonAsync("/api/catalog/categories", new { name = $"Category {Guid.NewGuid():N}" });
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var productResponse = await client.PostAsJsonAsync("/api/catalog/products", new
        {
            sku = $"SKU-{Guid.NewGuid():N}"[..20],
            name,
            categoryId,
            currentPrice = price,
            currency = "BRL",
        });
        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = product.GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/catalog/products/{productId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (productId, product.GetProperty("slug").GetString()!);
    }

    private async Task SeedStockAsync(Guid productId, int quantity)
    {
        await using var inventoryDb = new InventoryDbContext(Options<InventoryDbContext>(_postgres.GetConnectionString()));
        var stockItems = new EfStockItemRepository(inventoryDb);
        var unitOfWork = new InventoryUnitOfWork(
            inventoryDb, stockItems, new EfInventoryReservationRepository(inventoryDb), new NoOpDomainEventDispatcher());
        await stockItems.AddAsync(StockItem.Create(productId, quantity, null, DateTimeOffset.UtcNow), CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private static Task<HttpResponseMessage> CheckoutAsync(
        HttpClient client, Guid customerId, Guid addressId, Guid productId, int quantity, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout")
        {
            Content = JsonContent.Create(new
            {
                customerId,
                items = new[] { new { productId, quantity } },
                shippingAddressId = addressId,
                billingAddressId = addressId,
                paymentMethod = "Pix",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    /// <summary>
    /// The outbox publisher polls every 5 seconds, so the outcome can take
    /// a few seconds to land. This is the same polling the storefront does.
    /// </summary>
    private static async Task<JsonElement> PollOrderUntilAsync(HttpClient client, Guid orderId, Func<string, bool> isDone)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            var order = await client.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
            if (isDone(order.GetProperty("status").GetString()!))
            {
                return order;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Order {orderId} is still '{order.GetProperty("status").GetString()}'.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
