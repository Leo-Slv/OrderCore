using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// The payment window through the real host, job and outbox, with a
/// two-second window instead of thirty minutes: a buyer who never confirms
/// the card ends with the order <c>PaymentFailed</c> and its stock released.
/// </summary>
public sealed class PaymentWindowFlowTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public PaymentWindowFlowTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task A_buyer_who_never_confirms_the_card_ends_the_order_and_frees_the_stock()
    {
        var provider = new WaitingForBuyerProvider();
        await using var factory = _database.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Payments:PaymentWindow:Window", "00:00:02");
            builder.UseSetting("Payments:PaymentWindow:CheckInterval", "00:00:00.500");
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentProvider>(provider));
        });
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Abandoned Vase", 25m);
        await _database.SeedStockAsync(product.Id, 4);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, 3, $"window-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityAvailable").GetInt32().Should().Be(1, "3 units are held while the buyer confirms the card");

        var order = await PollOrderUntilAsync(customer, orderId, status => status == "PaymentFailed");

        order.GetProperty("payment").GetProperty("failureReason").GetString().Should().Be("payment_window_expired");
        provider.Cancellations.Should().Be(1, "the intent is cancelled so the card can no longer be confirmed");
        stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityAvailable").GetInt32().Should().Be(4);
    }
}
