using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace OrderCore.IntegrationTests.Catalog;

/// <summary>
/// <c>dotnet OrderCore.Api.dll seed-demo</c>, run through the API's real entry
/// point against an empty database: the Marfim storefront's four categories
/// and eight published pieces appear in the storefront listing, with their
/// atelier as brand, the promotions' "de" prices and every availability state;
/// running it again changes nothing.
/// </summary>
public sealed class SeedDemoCommandTests : IAsyncLifetime
{
    private readonly ApiDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private int SeedDemo() =>
        (int)typeof(Program).Assembly.EntryPoint!.Invoke(
            null, [new[] { "seed-demo", $"--ConnectionStrings:OrderCoreDb={_database.ConnectionString}" }])!;

    private static async Task<List<JsonElement>> StorefrontProductsAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?active=true&pageSize=100", ApiDatabase.Json);
        return page.GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Seed_demo_publishes_the_storefront_catalog_and_is_safe_to_repeat()
    {
        SeedDemo().Should().Be(0);

        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();
        var categories = await client.GetFromJsonAsync<List<JsonElement>>("/api/catalog/categories", ApiDatabase.Json);
        categories!.Select(c => c.GetProperty("name").GetString()).Should().BeEquivalentTo("Casa", "Cozinha", "Iluminação", "Têxteis");

        var products = await StorefrontProductsAsync(client);
        products.Should().HaveCount(8);
        var bySku = products.ToDictionary(p => p.GetProperty("sku").GetString()!);

        var arco = bySku["MF-ARCO"];
        arco.GetProperty("name").GetString().Should().Be("Luminária Arco");
        arco.GetProperty("currentPrice").GetDecimal().Should().Be(489m);
        arco.GetProperty("brand").GetString().Should().Be("Oficina Faísca");
        arco.GetProperty("availability").GetString().Should().Be("InStock");

        bySku["MF-ORBE"].GetProperty("compareAtPrice").GetDecimal().Should().Be(449m);
        bySku["MF-LINA"].GetProperty("compareAtPrice").GetDecimal().Should().Be(1490m);
        bySku["MF-LINA"].GetProperty("availability").GetString().Should().Be("LowStock");
        bySku["MF-SEIXO"].GetProperty("availability").GetString().Should().Be("OutOfStock");

        var onSale = await client.GetFromJsonAsync<JsonElement>("/api/catalog/products?active=true&onSale=true", ApiDatabase.Json);
        onSale.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("sku").GetString()).Should().BeEquivalentTo("MF-ORBE", "MF-LINA");

        SeedDemo().Should().Be(0, "running it again only adds what is missing");
        (await StorefrontProductsAsync(client)).Should().HaveCount(8);
        (await client.GetFromJsonAsync<List<JsonElement>>("/api/catalog/categories", ApiDatabase.Json)).Should().HaveCount(4);
    }

    [Fact]
    public void Seed_demo_fails_with_a_non_zero_exit_when_the_database_is_unreachable()
    {
        var exit = (int)typeof(Program).Assembly.EntryPoint!.Invoke(
            null, [new[] { "seed-demo", "--ConnectionStrings:OrderCoreDb=Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2" }])!;

        exit.Should().Be(1);
    }
}
