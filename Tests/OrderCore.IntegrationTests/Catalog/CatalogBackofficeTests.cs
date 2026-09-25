using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Catalog;

/// <summary>
/// The Catalog backoffice through the real host and database: a new
/// product gets its stock record in Inventory, the admin list shows and
/// filters stock, images and variants can be added to an already-saved
/// product, and promotions and discontinuing show on the storefront.
/// </summary>
public sealed class CatalogBackofficeTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public CatalogBackofficeTests(ApiDatabase database)
    {
        _database = database;
    }

    private static async Task<List<JsonElement>> AdminListAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync($"/api/admin/catalog/products?pageSize=100&{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task A_new_product_has_an_empty_stock_record_and_shows_as_out_of_stock_in_the_admin_list()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Stockless Vase", 30m);

        var stock = await admin.GetFromJsonAsync<JsonElement>($"/api/inventory/stock-items/{product.Id}", Json);
        stock.GetProperty("quantityOnHand").GetInt32().Should().Be(0);

        var outOfStock = await AdminListAsync(admin, "stock=OutOfStock");
        var row = outOfStock.Single(p => p.GetProperty("id").GetGuid() == product.Id);
        row.GetProperty("status").GetString().Should().Be("Active");
        row.GetProperty("stock").GetProperty("state").GetString().Should().Be("OutOfStock");

        await admin.PostAsJsonAsync($"/api/inventory/stock-items/{product.Id}/receive", new { quantity = 10 });

        var inStock = await AdminListAsync(admin, "stock=InStock");
        inStock.Single(p => p.GetProperty("id").GetGuid() == product.Id)
            .GetProperty("stock").GetProperty("quantityAvailable").GetInt32().Should().Be(10);
        (await AdminListAsync(admin, "stock=OutOfStock")).Should().NotContain(p => p.GetProperty("id").GetGuid() == product.Id);
    }

    [Fact]
    public async Task The_admin_list_finds_drafts_by_sku()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var categoryId = (await (await admin.PostAsJsonAsync("/api/catalog/categories", new { name = $"Category {Guid.NewGuid():N}" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var sku = $"DRAFT-{Guid.NewGuid():N}"[..16];
        (await admin.PostAsJsonAsync("/api/catalog/products", new { sku, name = "Unfinished Lamp", categoryId, currentPrice = 70m, currency = "BRL" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var found = await AdminListAsync(admin, $"searchTerm={sku.ToLowerInvariant()}&status=Draft");

        found.Should().ContainSingle().Which.GetProperty("name").GetString().Should().Be("Unfinished Lamp");
    }

    [Fact]
    public async Task Images_and_variants_are_added_to_a_saved_product_and_reordered()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Gallery Chair", 300m);
        var basePath = $"/api/catalog/products/{product.Id}";

        (await admin.PostAsJsonAsync($"{basePath}/images", new { url = "https://cdn.example.com/front.png", altText = "Front", isPrimary = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var withTwo = await (await admin.PostAsJsonAsync($"{basePath}/images", new { url = "https://cdn.example.com/side.png" }))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var imageIds = withTwo.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();

        var reordered = await admin.PutAsJsonAsync($"{basePath}/images/order", new { imageIds = imageIds.AsEnumerable().Reverse() });
        reordered.StatusCode.Should().Be(HttpStatusCode.OK);

        var variant = await admin.PostAsJsonAsync($"{basePath}/variants", new
        {
            sku = "CHAIR-OAK",
            name = "Oak",
            attributes = new Dictionary<string, string> { ["wood"] = "oak" },
            additionalPrice = 40m,
        });
        variant.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/catalog/products/by-slug/{product.Slug}", Json);
        page.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("url").GetString())
            .Should().Equal("https://cdn.example.com/side.png", "https://cdn.example.com/front.png");
        page.GetProperty("variants").EnumerateArray().Single().GetProperty("sku").GetString().Should().Be("CHAIR-OAK");

        var removed = await admin.DeleteAsync($"{basePath}/images/{imageIds[0]}");
        (await removed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("images").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task A_promotion_shows_on_the_storefront_and_discontinuing_takes_the_product_down()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var product = await CreatePublishedProductAsync(admin, "Promo Blender", 200m);
        await _database.SeedStockAsync(product.Id, quantity: 3);
        var basePath = $"/api/catalog/products/{product.Id}";
        var storefront = factory.CreateClient();

        (await admin.PutAsJsonAsync($"{basePath}/price", new { newPrice = 150m })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PutAsJsonAsync($"{basePath}/compare-at-price", new { compareAtPrice = 200m })).StatusCode.Should().Be(HttpStatusCode.OK);

        var onSale = await storefront.GetFromJsonAsync<JsonElement>("/api/catalog/products?onSale=true&pageSize=100", Json);
        var listed = onSale.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == product.Id);
        listed.GetProperty("currentPrice").GetDecimal().Should().Be(150m);
        listed.GetProperty("compareAtPrice").GetDecimal().Should().Be(200m);

        var invalid = await admin.PutAsJsonAsync($"{basePath}/compare-at-price", new { compareAtPrice = 100m });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("invalid_compare_at_price");

        (await admin.PostAsync($"{basePath}/discontinue", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await storefront.GetAsync($"/api/catalog/products/by-slug/{product.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AdminListAsync(admin, "status=Discontinued")).Should().Contain(p => p.GetProperty("id").GetGuid() == product.Id);
    }

    [Fact]
    public async Task Product_operations_are_admin_only()
    {
        await using var factory = _database.CreateFactory();
        var (customer, _) = await SignUpCustomerAsync(factory);

        (await customer.GetAsync("/api/admin/catalog/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PostAsync($"/api/catalog/products/{Guid.NewGuid()}/discontinue", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
