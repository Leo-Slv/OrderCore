using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Customers;

/// <summary>
/// The customer's own profile and addresses under <c>customers/me</c>, and
/// that one customer can't touch another's addresses.
/// </summary>
public sealed class MyAccountTests : IClassFixture<ApiDatabase>, IAsyncLifetime
{
    private readonly ApiDatabase _database;
    private WebApplicationFactory<Program> _factory = null!;

    public MyAccountTests(ApiDatabase database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _factory = _database.CreateFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static object AddressBody(string label, string street) => new
    {
        label,
        recipientName = "Jane Doe",
        street,
        number = "42",
        neighborhood = "Centro",
        city = "São Paulo",
        state = "SP",
        postalCode = "01000-000",
        country = "BR",
    };

    [Fact]
    public async Task A_customer_updates_their_own_profile()
    {
        var (customer, _) = await SignUpCustomerAsync(_factory);

        var update = await customer.PutAsJsonAsync("/api/customers/me", new { name = "Jane Updated", phone = "+55 11 99999-0000" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.GetFromJsonAsync<JsonElement>("/api/customers/me", Json)).GetProperty("name").GetString().Should().Be("Jane Updated");
    }

    [Fact]
    public async Task A_customer_manages_their_own_addresses_and_defaults()
    {
        var (customer, _) = await SignUpCustomerAsync(_factory);
        var homeId = await AddAddressAsync(customer, "Rua A");
        var workId = await AddAddressAsync(customer, "Rua B");

        var edit = await customer.PutAsJsonAsync($"/api/customers/me/addresses/{homeId}", AddressBody("Casa", "Rua Nova"));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.PostAsync($"/api/customers/me/addresses/{workId}/default-shipping", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await customer.PostAsync($"/api/customers/me/addresses/{homeId}/default-billing", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var addresses = (await customer.GetFromJsonAsync<JsonElement>("/api/customers/me/addresses", Json)).EnumerateArray().ToList();
        var home = addresses.Single(a => a.GetProperty("id").GetGuid() == homeId);
        home.GetProperty("label").GetString().Should().Be("Casa");
        home.GetProperty("street").GetString().Should().Be("Rua Nova");
        home.GetProperty("isDefaultBilling").GetBoolean().Should().BeTrue();
        addresses.Single(a => a.GetProperty("id").GetGuid() == workId).GetProperty("isDefaultShipping").GetBoolean().Should().BeTrue();

        (await customer.DeleteAsync($"/api/customers/me/addresses/{workId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await customer.GetFromJsonAsync<JsonElement>("/api/customers/me/addresses", Json)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task A_customer_cannot_touch_another_customers_address()
    {
        var (owner, _) = await SignUpCustomerAsync(_factory, "Owner");
        var (intruder, _) = await SignUpCustomerAsync(_factory, "Intruder");
        var addressId = await AddAddressAsync(owner);

        var edit = await intruder.PutAsJsonAsync($"/api/customers/me/addresses/{addressId}", AddressBody("Mine now", "Rua X"));
        var remove = await intruder.DeleteAsync($"/api/customers/me/addresses/{addressId}");
        var makeDefault = await intruder.PostAsync($"/api/customers/me/addresses/{addressId}/default-shipping", null);

        edit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        remove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        makeDefault.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetFromJsonAsync<JsonElement>("/api/customers/me/addresses", Json))[0]
            .GetProperty("street").GetString().Should().Be("Rua das Flores");
    }

    [Fact]
    public async Task Checkout_refuses_an_address_that_belongs_to_another_customer()
    {
        var admin = await SignInAsAdminAsync(_factory);
        var (owner, _) = await SignUpCustomerAsync(_factory, "Owner");
        var (buyer, _) = await SignUpCustomerAsync(_factory, "Buyer");
        var ownersAddress = await AddAddressAsync(owner);
        var product = await CreatePublishedProductAsync(admin, $"Product {Guid.NewGuid():N}", price: 10m);
        await _database.SeedStockAsync(product.Id, quantity: 5);

        var checkout = await CheckoutAsync(buyer, ownersAddress, product.Id, quantity: 1, idempotencyKey: $"cross-{Guid.NewGuid():N}");

        checkout.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await checkout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("address_not_found");
    }
}
