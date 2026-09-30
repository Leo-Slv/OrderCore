using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Payments.Domain.Repositories;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// <c>POST payments/{id}/reconcile</c> through the real host: the buyer
/// confirmed the card but the webhook never arrived — asking the provider
/// recovers it and the order is confirmed; asking again changes nothing.
/// </summary>
public sealed class ReconciliationTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public ReconciliationTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task A_lost_webhook_is_recovered_by_reconciling_and_a_second_run_changes_nothing()
    {
        var provider = new WaitingForBuyerProvider();
        await using var factory = _database.CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentProvider>(provider)));
        var admin = await SignInAsAdminAsync(factory);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var product = await CreatePublishedProductAsync(admin, "Reconciled Rug", 120m);
        await _database.SeedStockAsync(product.Id, 2);
        var checkout = await CheckoutAsync(customer, addressId, product.Id, 1, $"reconcile-{Guid.NewGuid():N}");
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var payment = await admin.GetFromJsonAsync<JsonElement>($"/api/payments/orders/{orderId}", Json);
        var paymentId = payment.GetProperty("id").GetGuid();

        // In sync while the buyer hasn't confirmed.
        var inSync = await ReconcileAsync(admin, paymentId);
        inSync.GetProperty("changed").GetBoolean().Should().BeFalse();
        inSync.GetProperty("providerStatus").GetString().Should().Be("WaitingForBuyer");

        // The buyer confirmed; the webhook was lost.
        provider.States[payment.GetProperty("providerReference").GetString()!] =
            new PaymentProviderState(PaymentProviderStatus.Authorized, AuthorizationExpiresAt: DateTimeOffset.UtcNow.AddDays(6));

        var recovered = await ReconcileAsync(admin, paymentId);

        recovered.GetProperty("changed").GetBoolean().Should().BeTrue();
        recovered.GetProperty("statusBefore").GetString().Should().Be("Processing");
        recovered.GetProperty("statusAfter").GetString().Should().Be("Authorized");
        await PollOrderUntilAsync(customer, orderId, status => status == "Confirmed");

        (await ReconcileAsync(admin, paymentId)).GetProperty("changed").GetBoolean().Should().BeFalse();
        var audit = await admin.GetFromJsonAsync<JsonElement>($"/api/audit-logs?entityName=Payment&entityId={paymentId}", Json);
        audit.GetProperty("items").EnumerateArray().Count(e => e.GetProperty("action").GetString() == "PaymentReconciled").Should().Be(1);
    }

    [Fact]
    public async Task Reconciling_a_payment_that_does_not_exist_is_not_found()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);

        var response = await admin.PostAsync($"/api/payments/{Guid.NewGuid()}/reconcile", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<JsonElement> ReconcileAsync(HttpClient admin, Guid paymentId)
    {
        var response = await admin.PostAsync($"/api/payments/{paymentId}/reconcile", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }
}
