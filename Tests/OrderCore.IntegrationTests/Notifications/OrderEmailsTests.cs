using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderCore.Api.Modules.Orders.Infrastructure.Persistence;
using OrderCore.Api.Modules.Payments.Infrastructure.Providers.Fake;
using Xunit;

namespace OrderCore.IntegrationTests.Notifications;

/// <summary>
/// The order e-mails through the real host, broker and SMTP (Mailpit): Orders'
/// events reach Notifications on <c>notifications.order-emails</c> and the
/// customer receives, in Portuguese, the confirmation, the shipment (with its
/// tracking link), the cancellation and the payment failure — reasons as
/// friendly text, never the raw code. A redelivered event sends nothing new
/// (Docs/specs/identity/password-recovery.md, item 6).
/// </summary>
public sealed class OrderEmailsTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public OrderEmailsTests(ApiDatabase database)
    {
        _database = database;
    }

    private async Task<(HttpClient Customer, string Email, Guid OrderId, string OrderNumber)> CheckOutAsync(
        WebApplicationFactory<Program> factory)
    {
        var admin = await ApiDatabase.SignInAsAdminAsync(factory);
        var (productId, _) = await ApiDatabase.CreatePublishedProductAsync(admin, "Caneca", 1234.5m);
        await _database.SeedStockAsync(productId, 5);
        var (customer, _) = await ApiDatabase.SignUpCustomerAsync(factory, name: "Maria Silva");
        var email = (await customer.GetFromJsonAsync<JsonElement>("/api/customers/me")).GetProperty("email").GetString()!;
        var addressId = await ApiDatabase.AddAddressAsync(customer);

        var checkout = await ApiDatabase.CheckoutAsync(customer, addressId, productId, 1, $"order-emails-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var order = await checkout.Content.ReadFromJsonAsync<JsonElement>(ApiDatabase.Json);
        return (customer, email, order.GetProperty("id").GetGuid(), order.GetProperty("orderNumber").GetString()!);
    }

    [Fact]
    public async Task A_confirmed_and_shipped_order_emails_the_customer_with_the_tracking_link()
    {
        await using var factory = _database.CreateFactory();
        var (customer, email, orderId, orderNumber) = await CheckOutAsync(factory);
        await ApiDatabase.PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        var confirmed = await TestMailpit.WaitForMessageAsync(email, subject: $"Pedido {orderNumber} confirmado");
        confirmed.Html.Should().Contain("Olá, Maria Silva.").And.Contain("R$ 1.234,50");

        var admin = await ApiDatabase.SignInAsAdminAsync(factory);
        (await admin.PostAsync($"/api/orders/{orderId}/start-processing", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/orders/{orderId}/ship", new
        {
            carrier = "Correios",
            trackingCode = "AB123456789BR",
            trackingUrl = "https://rastreamento.correios.com.br/app/index.php?objeto=AB123456789BR",
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var shipped = await TestMailpit.WaitForMessageAsync(email, subject: $"Pedido {orderNumber} enviado");
        shipped.Html.Should().Contain("Transportadora: Correios. Código de rastreio: AB123456789BR.")
            .And.Contain("href=\"https://rastreamento.correios.com.br/app/index.php?objeto=AB123456789BR\"");
        shipped.Text.Should().Contain("Acompanhe a entrega: https://rastreamento.correios.com.br/app/index.php?objeto=AB123456789BR");
    }

    [Fact]
    public async Task A_cancelled_order_emails_a_friendly_reason_and_a_redelivery_sends_nothing_new()
    {
        await using var factory = _database.CreateFactory();
        var (customer, email, orderId, orderNumber) = await CheckOutAsync(factory);
        await ApiDatabase.PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        (await customer.PostAsJsonAsync($"/api/orders/me/{orderId}/cancel", new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        var subject = $"Pedido {orderNumber} cancelado";
        var cancelled = await TestMailpit.WaitForMessageAsync(email, subject: subject);
        cancelled.Html.Should().Contain("O pedido foi cancelado a seu pedido.").And.NotContain("Cancelled by the customer");

        var published = await _database.WaitForPublishedEventAsync<OrdersDbContext>("orders.order-cancelled", orderId);
        await TestBroker.RedeliverAsync(factory, published);
        await Task.Delay(TimeSpan.FromSeconds(2));
        (await TestMailpit.CountAsync(email, subject)).Should().Be(1, "the inbox turns the redelivery into a no-op");
    }

    [Fact]
    public async Task A_declined_payment_emails_the_customer_without_the_raw_code()
    {
        await using var factory = _database.CreateFactory(FakePaymentProviderMode.Declined);
        var (customer, email, orderId, orderNumber) = await CheckOutAsync(factory);
        await ApiDatabase.PollOrderUntilAsync(customer, orderId, status => status == "PaymentFailed");

        var failed = await TestMailpit.WaitForMessageAsync(email, subject: $"Pagamento do pedido {orderNumber} não aprovado");

        failed.Html.Should().Contain("Nenhum valor foi cobrado").And.NotContainAny("_declined", "card_");
    }
}
