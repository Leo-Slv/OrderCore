using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// Checkout with a provider that needs the buyer to confirm the card, as
/// Stripe does, through the real host and database: the response carries
/// the confirmation step, a replay carries it again, the order waits in
/// <c>PendingPayment</c>, and cancelling it cancels the payment at the
/// provider and puts the stock back.
/// </summary>
public sealed class CardConfirmationCheckoutTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public CardConfirmationCheckoutTests(ApiDatabase database)
    {
        _database = database;
    }

    private WebApplicationFactory<Program> Factory(WaitingForBuyerProvider provider) =>
        _database.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentProvider>(provider)));

    [Fact]
    public async Task The_checkout_hands_out_the_card_confirmation_and_cancelling_cancels_the_payment()
    {
        var provider = new WaitingForBuyerProvider();
        await using var factory = Factory(provider);
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Confirmed Card Mug", 30m);
        await _database.SeedStockAsync(product.Id, 3);
        var key = $"card-{Guid.NewGuid():N}";

        var checkout = await CheckoutAsync(customer, addressId, product.Id, 1, key);

        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var order = await checkout.Content.ReadFromJsonAsync<JsonElement>(Json);
        var orderId = order.GetProperty("id").GetGuid();
        order.GetProperty("status").GetString().Should().Be("PendingPayment");
        var payment = order.GetProperty("payment");
        payment.GetProperty("status").GetString().Should().Be("Processing");
        var nextAction = payment.GetProperty("nextAction");
        nextAction.GetProperty("type").GetString().Should().Be("confirm_card");
        nextAction.GetProperty("clientSecret").GetString().Should().Be(WaitingForBuyerProvider.Secret);

        // A replay hands the same step out again, asked of the provider, without a second payment.
        var replay = await CheckoutAsync(customer, addressId, product.Id, 1, key);
        var replayed = await replay.Content.ReadFromJsonAsync<JsonElement>(Json);
        replayed.GetProperty("id").GetGuid().Should().Be(orderId);
        replayed.GetProperty("payment").GetProperty("nextAction").GetProperty("clientSecret").GetString()
            .Should().Be(WaitingForBuyerProvider.Secret);
        provider.Authorizations.Should().Be(1);

        // The order keeps waiting (reading it again carries no secret) until the buyer confirms...
        var read = await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json);
        read.GetProperty("status").GetString().Should().Be("PendingPayment");
        read.GetProperty("payment").GetProperty("nextAction").ValueKind.Should().Be(JsonValueKind.Null);

        // ...or cancels: the payment is cancelled at the provider and voided, the stock comes back.
        var cancel = await customer.PostAsJsonAsync($"/api/orders/me/{orderId}/cancel", new { });

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("paymentSettlement").GetString().Should().Be("Voided");
        provider.Cancellations.Should().Be(1);
        (await admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json)).GetProperty("status").GetString()
            .Should().Be("Voided");
        var stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityAvailable").GetInt32().Should().Be(3);
    }
}
