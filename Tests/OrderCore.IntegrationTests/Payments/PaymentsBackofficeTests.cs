using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Payments;

/// <summary>
/// The backoffice payment list and detail through the real host: the
/// filters bind from the query string by name, and a checkout's payment is
/// listed and readable in full.
/// </summary>
public sealed class PaymentsBackofficeTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public PaymentsBackofficeTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_admin_finds_a_checkouts_payment_in_the_list_and_reads_it()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Listed Kettle", 120m);
        await _database.SeedStockAsync(product.Id, quantity: 3);
        var (customer, _) = await SignUpCustomerAsync(factory);
        var addressId = await AddAddressAsync(customer);
        var checkout = await CheckoutAsync(customer, addressId, product.Id, quantity: 1, idempotencyKey: "payments-list-1");
        checkout.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/payments?status=Authorized&method=Pix&pageSize=100", Json);
        var row = list.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("orderId").GetGuid() == orderId);
        row.GetProperty("amount").GetDecimal().Should().Be(120m);
        row.GetProperty("refundedAmount").GetDecimal().Should().Be(0m);

        var cardOnly = await admin.GetFromJsonAsync<JsonElement>("/api/payments?method=Card&pageSize=100", Json);
        cardOnly.GetProperty("items").EnumerateArray().Should().NotContain(p => p.GetProperty("orderId").GetGuid() == orderId);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/payments/{row.GetProperty("id").GetGuid()}", Json);
        detail.GetProperty("provider").GetString().Should().NotBeNullOrEmpty();
        detail.GetProperty("providerReference").GetString().Should().NotBeNullOrEmpty();
        detail.GetProperty("refunds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_payment_is_not_found_and_an_unknown_status_is_a_validation_error()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);

        var missing = await admin.GetAsync($"/api/payments/{Guid.NewGuid()}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("payment_not_found");

        var badFilter = await admin.GetAsync("/api/payments?status=Teleported");
        badFilter.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
