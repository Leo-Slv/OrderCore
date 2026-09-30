using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.AuditLogs.Infrastructure.Persistence;
using OrderCore.Api.Modules.Catalog.Infrastructure.Persistence;
using OrderCore.Api.Modules.Customers.Infrastructure.Persistence;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Domain.Entities;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence;
using OrderCore.Api.Modules.Inventory.Infrastructure.Persistence.Repositories;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence;
using OrderCore.Api.Modules.Notifications.Infrastructure.Persistence;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using OrderCore.Api.Shared.Application.Abstractions;
using OrderCore.Api.Shared.Domain;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderCore.IntegrationTests;

/// <summary>
/// A PostgreSQL container with every module's migrations applied, and the
/// real API host pointed at it with a seeded admin. Also holds the HTTP
/// steps most API-level tests share: sign in as the admin, sign a customer
/// up, publish a product, check out. Use it as a class fixture to share one
/// database across a test class, or hold one per test for a clean database
/// each time.
/// </summary>
public sealed class ApiDatabase : IAsyncLifetime
{
    public const string AdminEmail = "admin@ordercore.test";
    public const string AdminPassword = "admin-pass-123";
    public const string CustomerPassword = "buyer-pass-123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await MigrateAsync<CustomersDbContext>(options => new(options));
        await MigrateAsync<CatalogDbContext>(options => new(options));
        await MigrateAsync<InventoryDbContext>(options => new(options));
        await MigrateAsync<OrdersDbContext>(options => new(options));
        await MigrateAsync<PaymentsDbContext>(options => new(options));
        await MigrateAsync<IdentityDbContext>(options => new(options));
        await MigrateAsync<AuditLogsDbContext>(options => new(options));
        await MigrateAsync<MessagingDbContext>(options => new(options));
        await MigrateAsync<NotificationsDbContext>(options => new(options));
        await TestMailpit.StartAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public DbContextOptions<T> Options<T>()
        where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(ConnectionString).Options;

    public WebApplicationFactory<Program> CreateFactory(FakePaymentProviderMode paymentMode = FakePaymentProviderMode.Success) =>
        new OrderCoreApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:OrderCoreDb", ConnectionString);
            builder.UseSetting("IdentitySeed:AdminEmail", AdminEmail);
            builder.UseSetting("IdentitySeed:AdminPassword", AdminPassword);

            // E-mail goes to the shared Mailpit, never to a developer's own.
            builder.UseSetting("Notifications:Smtp:Host", TestMailpit.Host);
            builder.UseSetting("Notifications:Smtp:Port", TestMailpit.Port.ToString(CultureInfo.InvariantCulture));
            builder.ConfigureTestServices(services =>
                services.Configure<FakePaymentProviderOptions>(options => options.Mode = paymentMode));
        });

    public static async Task<HttpClient> SignInAsAdminAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email = AdminEmail, password = AdminPassword });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UseAccessToken(client, await response.Content.ReadFromJsonAsync<JsonElement>(Json));
        return client;
    }

    /// <summary>A new customer account (unique e-mail), with the client already carrying its access token.</summary>
    public static async Task<(HttpClient Client, JsonElement Tokens)> SignUpCustomerAsync(
        WebApplicationFactory<Program> factory, string name = "Jane Doe")
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/sign-up", new
        {
            name,
            email = $"customer-{Guid.NewGuid():N}@example.com",
            password = CustomerPassword,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        UseAccessToken(client, tokens);
        return (client, tokens);
    }

    /// <summary>Saves an address as the signed-in customer and returns its id.</summary>
    public static async Task<Guid> AddAddressAsync(HttpClient customer, string street = "Rua das Flores")
    {
        var response = await customer.PostAsJsonAsync("/api/customers/me/addresses", new
        {
            label = "Home",
            recipientName = "Jane Doe",
            street,
            number = "42",
            neighborhood = "Centro",
            city = "São Paulo",
            state = "SP",
            postalCode = "01000-000",
            country = "BR",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
    }

    public static async Task<(Guid Id, string Slug)> CreatePublishedProductAsync(HttpClient admin, string name, decimal price)
    {
        var categoryResponse = await admin.PostAsJsonAsync("/api/catalog/categories", new { name = $"Category {Guid.NewGuid():N}" });
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var productResponse = await admin.PostAsJsonAsync("/api/catalog/products", new
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

        (await admin.PostAsync($"/api/catalog/products/{productId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        return (productId, product.GetProperty("slug").GetString()!);
    }

    /// <summary>
    /// Directly in the database, for tests about something else than
    /// receiving stock: puts <paramref name="quantity"/> units in the
    /// product's stock record (created with the product), or creates the
    /// record for a product that never went through the catalog.
    /// </summary>
    public async Task SeedStockAsync(Guid productId, int quantity)
    {
        await using var inventoryDb = new InventoryDbContext(Options<InventoryDbContext>());
        var stockItems = new EfStockItemRepository(inventoryDb);
        var unitOfWork = new InventoryUnitOfWork(
            inventoryDb, stockItems, new EfInventoryReservationRepository(inventoryDb), new NoOpDomainEventDispatcher(), TestOutboxes.Inventory(inventoryDb));
        var stockItem = await stockItems.GetByProductIdAsync(productId, CancellationToken.None);
        if (stockItem is null)
        {
            await stockItems.AddAsync(StockItem.Create(productId, quantity, null, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        else if (quantity > 0)
        {
            stockItem.Receive(quantity, "test seed", DateTimeOffset.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    public static Task<HttpResponseMessage> CheckoutAsync(
        HttpClient customer, Guid addressId, Guid productId, int quantity, string idempotencyKey, string? traceParent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/checkout")
        {
            Content = JsonContent.Create(new
            {
                items = new[] { new { productId, quantity } },
                shippingAddressId = addressId,
                billingAddressId = addressId,
                paymentMethod = "Pix",
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (traceParent is not null)
        {
            request.Headers.Add("traceparent", traceParent);
        }

        return customer.SendAsync(request);
    }

    /// <summary>
    /// A payment outcome reaches the order through the outbox relay and
    /// RabbitMQ, so it can take a moment. This is the same polling the
    /// storefront does. Works for the order's owner and for an admin.
    /// </summary>
    public static async Task<JsonElement> PollOrderUntilAsync(HttpClient client, Guid orderId, Func<string, bool> isDone)
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

    /// <summary>
    /// Waits until the Messaging relay has published the event
    /// <paramref name="contract"/> about the order from the outbox in
    /// <typeparamref name="TDbContext"/>, and returns its outbox row.
    /// </summary>
    public async Task<OutboxMessage> WaitForPublishedEventAsync<TDbContext>(string contract, Guid orderId)
        where TDbContext : DbContext
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            await using (var db = (TDbContext)Activator.CreateInstance(typeof(TDbContext), Options<TDbContext>())!)
            {
                var published = await db.Set<OutboxMessage>().Where(m => m.Type == contract && m.SentAt != null).ToListAsync();
                var row = published.FirstOrDefault(m =>
                    MessageEnvelope.FromOutbox(m).Payload.GetProperty("orderId").GetGuid() == orderId);
                if (row is not null)
                {
                    return row;
                }
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"No published '{contract}' for order {orderId}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    /// <summary>Whether Orders' inbox records the message as handled by <paramref name="consumer"/>.</summary>
    public async Task<bool> OrdersHandledAsync(Guid messageId, string consumer)
    {
        await using var db = new OrdersDbContext(Options<OrdersDbContext>());
        return await db.Set<InboxMessage>().AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer);
    }

    public static void UseAccessToken(HttpClient client, JsonElement tokens) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

    private async Task MigrateAsync<T>(Func<DbContextOptions<T>, T> create)
        where T : DbContext
    {
        await using var db = create(Options<T>());
        await db.Database.MigrateAsync();
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
