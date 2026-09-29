using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Payments.Contracts.IntegrationEvents;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using OrderCore.Api.Modules.Payments.Infrastructure.Persistence;
using OrderCore.Api.Shared.Infrastructure.Messaging;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// Stripe's webhooks through the real host and database, signed the way
/// Stripe signs them (HMAC-SHA256 of <c>{timestamp}.{body}</c> with the
/// webhook secret): only a correctly signed, recent event is accepted, the
/// same event twice changes nothing, and each event moves the payment — and
/// through the outbox, the order — the way Stripe's answer means.
/// </summary>
public sealed class StripeWebhookTests : IClassFixture<ApiDatabase>
{
    private const string WebhookSecret = "whsec_ordercore_test";
    private const string Endpoint = "/api/payments/webhooks/stripe";

    private readonly ApiDatabase _database;

    public StripeWebhookTests(ApiDatabase database)
    {
        _database = database;
    }

    private WebApplicationFactory<Program> Factory(string? webhookSecret = WebhookSecret) =>
        _database.CreateFactory().WithWebHostBuilder(builder =>
        {
            if (webhookSecret is not null)
            {
                builder.UseSetting("Payments:Stripe:WebhookSecret", webhookSecret);
            }

            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentProvider>(new WaitingForBuyerProvider()));
        });

    /// <summary>A customer's order whose payment waits for the buyer; returns its PaymentIntent id.</summary>
    private async Task<(HttpClient Admin, HttpClient Customer, Guid OrderId, string Intent)> OrderWaitingForTheBuyerAsync(
        WebApplicationFactory<Program> factory)
    {
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, $"Webhook Lamp {Guid.NewGuid():N}"[..20], 45m);
        await _database.SeedStockAsync(product.Id, 5);

        var checkout = await CheckoutAsync(customer, addressId, product.Id, 1, $"webhook-{Guid.NewGuid():N}");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var payment = await PaymentAsync(admin, orderId);
        return (admin, customer, orderId, payment.GetProperty("providerReference").GetString()!);
    }

    private static Task<JsonElement> PaymentAsync(HttpClient admin, Guid orderId) =>
        admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json);

    [Fact]
    public async Task The_buyer_confirming_the_card_confirms_the_order_once()
    {
        await using var factory = Factory();
        var (admin, customer, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);
        var authorized = IntentEvent("payment_intent.amount_capturable_updated", intent, "requires_capture");

        (await PostSignedAsync(factory, authorized)).StatusCode.Should().Be(HttpStatusCode.OK);
        await PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        // Stripe delivers the same event again: accepted, and nothing happens twice.
        (await PostSignedAsync(factory, authorized)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PaymentAsync(admin, orderId)).GetProperty("status").GetString().Should().Be("Authorized");
        (await OutboxCountAsync(PaymentAuthorized.Name, orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Only_a_body_stripe_signed_recently_with_the_secret_is_accepted()
    {
        await using var factory = Factory();
        var (admin, _, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);
        var body = IntentEvent("payment_intent.amount_capturable_updated", intent, "requires_capture");
        var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        var attempts = new[]
        {
            ("tampered body", body.Replace("requires_capture", "succeeded"), Sign(body, WebhookSecret, now)),
            ("wrong secret", body, Sign(body, "whsec_someone_else", now)),
            ("an hour old", body, Sign(body, WebhookSecret, now.AddHours(-1))),
            ("unsigned", body, null),
        };

        foreach (var (attempt, payload, signature) in attempts)
        {
            var response = await PostAsync(client, payload, signature);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, attempt);
            (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString()
                .Should().Be("invalid_webhook_signature", attempt);
        }

        (await PaymentAsync(admin, orderId)).GetProperty("status").GetString().Should().Be("Processing");
    }

    [Fact]
    public async Task A_declined_card_keeps_the_order_waiting_until_stripe_gives_up()
    {
        await using var factory = Factory();
        var (admin, customer, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);

        (await PostSignedAsync(factory, IntentEvent("payment_intent.payment_failed", intent, "requires_payment_method", declineCode: "insufficient_funds")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var payment = await PaymentAsync(admin, orderId);
        payment.GetProperty("status").GetString().Should().Be("Processing");
        payment.GetProperty("lastDeclineReason").GetString().Should().Be("insufficient_funds");
        (await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json)).GetProperty("status").GetString()
            .Should().Be("PendingPayment");

        // Cancelled at Stripe while still waiting: the payment fails with the last decline and the order ends.
        await PostSignedAsync(factory, IntentEvent("payment_intent.canceled", intent, "canceled", cancellationReason: "abandoned"));

        var failed = await PollOrderUntilAsync(customer, orderId, status => status == "PaymentFailed");
        failed.GetProperty("payment").GetProperty("failureReason").GetString().Should().Be("insufficient_funds");
    }

    [Fact]
    public async Task A_late_authorized_for_a_cancelled_order_changes_nothing()
    {
        await using var factory = Factory();
        var (admin, customer, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);
        (await customer.PostAsJsonAsync($"/api/orders/me/{orderId}/cancel", new { })).StatusCode.Should().Be(HttpStatusCode.OK);

        var late = await PostSignedAsync(factory, IntentEvent("payment_intent.amount_capturable_updated", intent, "requires_capture"));

        late.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PaymentAsync(admin, orderId)).GetProperty("status").GetString().Should().Be("Voided");
        (await customer.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}", Json)).GetProperty("status").GetString()
            .Should().Be("Cancelled");
        (await OutboxCountAsync(PaymentAuthorized.Name, orderId)).Should().Be(0);
    }

    [Fact]
    public async Task An_expired_authorization_voids_the_payment_and_is_announced()
    {
        await using var factory = Factory();
        var (admin, customer, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);
        await PostSignedAsync(factory, IntentEvent("payment_intent.amount_capturable_updated", intent, "requires_capture"));
        await PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        await PostSignedAsync(factory, IntentEvent("payment_intent.canceled", intent, "canceled", cancellationReason: "automatic"));

        (await PaymentAsync(admin, orderId)).GetProperty("status").GetString().Should().Be("Voided");
        await _database.WaitForPublishedEventAsync<PaymentsDbContext>(PaymentAuthorizationExpired.Name, orderId);
    }

    [Fact]
    public async Task A_dispute_is_recorded_on_the_payment()
    {
        await using var factory = Factory();
        var (admin, customer, orderId, intent) = await OrderWaitingForTheBuyerAsync(factory);
        await PostSignedAsync(factory, IntentEvent("payment_intent.amount_capturable_updated", intent, "requires_capture"));
        await PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        var dispute = Event("charge.dispute.created", new JsonObject
        {
            ["id"] = "dp_1",
            ["object"] = "dispute",
            ["payment_intent"] = intent,
            ["reason"] = "fraudulent",
            ["status"] = "needs_response",
        });
        (await PostSignedAsync(factory, dispute)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await PaymentAsync(admin, orderId)).GetProperty("disputedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Events_orderCore_does_not_act_on_are_acknowledged()
    {
        await using var factory = Factory();

        var response = await PostSignedAsync(factory, Event("customer.created", new JsonObject { ["id"] = "cus_1", ["object"] = "customer" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Without_a_webhook_secret_the_endpoint_is_off()
    {
        await using var factory = Factory(webhookSecret: null);

        var response = await PostAsync(factory.CreateClient(), "{}", Sign("{}", WebhookSecret, DateTimeOffset.UtcNow));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("stripe_webhooks_disabled");
    }

    private static Task<HttpResponseMessage> PostSignedAsync(WebApplicationFactory<Program> factory, string body) =>
        PostAsync(factory.CreateClient(), body, Sign(body, WebhookSecret, DateTimeOffset.UtcNow));

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (signature is not null)
        {
            request.Headers.Add("Stripe-Signature", signature);
        }

        return client.SendAsync(request);
    }

    /// <summary>Stripe's scheme: <c>t={unix time},v1={hex HMAC-SHA256 of "{t}.{body}"}</c>.</summary>
    private static string Sign(string body, string secret, DateTimeOffset at)
    {
        var timestamp = at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(mac)}";
    }

    private static string IntentEvent(string type, string intent, string status, string? cancellationReason = null, string? declineCode = null)
    {
        var paymentIntent = new JsonObject
        {
            ["id"] = intent,
            ["object"] = "payment_intent",
            ["status"] = status,
            ["capture_method"] = "manual",
            ["cancellation_reason"] = cancellationReason,
        };
        if (declineCode is not null)
        {
            paymentIntent["last_payment_error"] = new JsonObject { ["type"] = "card_error", ["code"] = "card_declined", ["decline_code"] = declineCode };
        }

        return Event(type, paymentIntent);
    }

    /// <summary>A Stripe event with a fresh id, as Stripe would send it.</summary>
    private static string Event(string type, JsonObject dataObject) =>
        new JsonObject
        {
            ["id"] = $"evt_{Guid.NewGuid():N}",
            ["object"] = "event",
            ["api_version"] = "2025-06-30.basil",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["livemode"] = false,
            ["type"] = type,
            ["data"] = new JsonObject { ["object"] = dataObject },
        }.ToJsonString();

    private async Task<int> OutboxCountAsync(string contract, Guid orderId)
    {
        await using var db = new PaymentsDbContext(_database.Options<PaymentsDbContext>());
        var rows = await db.Set<OutboxMessage>().Where(m => m.Type == contract).ToListAsync();
        return rows.Count(m => MessageEnvelope.FromOutbox(m).Payload.GetProperty("orderId").GetGuid() == orderId);
    }
}
