using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Inventory;

/// <summary>
/// The Inventory backoffice endpoints through the real host: receiving
/// stock and setting a reorder level reach the movement history and make
/// the storefront's <c>LowStock</c> real.
/// </summary>
public sealed class InventoryBackofficeTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public InventoryBackofficeTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_admin_receives_stock_sets_a_reorder_level_and_the_storefront_shows_low_stock()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Restocked Teapot", 89.90m);
        await _database.SeedStockAsync(product.Id, quantity: 0);

        var received = await admin.PostAsJsonAsync(
            $"/api/inventory/stock-items/{product.Id}/receive", new { quantity = 4, reason = "invoice 42" });
        received.StatusCode.Should().Be(HttpStatusCode.OK);
        (await received.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("state").GetString().Should().Be("InStock");

        var leveled = await admin.PutAsJsonAsync($"/api/inventory/stock-items/{product.Id}/reorder-level", new { reorderLevel = 5 });
        leveled.StatusCode.Should().Be(HttpStatusCode.OK);
        var stock = await leveled.Content.ReadFromJsonAsync<JsonElement>(Json);
        stock.GetProperty("state").GetString().Should().Be("LowStock");
        stock.GetProperty("reorderLevel").GetInt32().Should().Be(5);

        var lowList = await admin.GetFromJsonAsync<JsonElement>("/api/inventory/stock-items?state=LowStock&pageSize=100", Json);
        lowList.GetProperty("items").EnumerateArray().Should().Contain(s => s.GetProperty("productId").GetGuid() == product.Id);

        var movements = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}/movements", Json);
        var inbound = movements.GetProperty("items").EnumerateArray().Single();
        inbound.GetProperty("movementType").GetString().Should().Be("Inbound");
        inbound.GetProperty("quantity").GetInt32().Should().Be(4);
        inbound.GetProperty("reason").GetString().Should().Be("invoice 42");

        var storefront = factory.CreateClient();
        var page = await storefront.GetFromJsonAsync<JsonElement>($"/api/catalog/products/by-slug/{product.Slug}", Json);
        page.GetProperty("availability").GetString().Should().Be("LowStock");
    }

    [Fact]
    public async Task Receiving_nothing_is_a_validation_error()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Empty Box", 10m);
        await _database.SeedStockAsync(product.Id, quantity: 1);

        var response = await admin.PostAsJsonAsync($"/api/inventory/stock-items/{product.Id}/receive", new { quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("validation_error");
    }
}
