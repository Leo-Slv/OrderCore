using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderCore.IntegrationTests.Orders;

/// <summary>
/// Smoke test proving the OpenAPI document and the Scalar UI are actually
/// mapped in Development — see the note in Program.cs on why both are
/// gated to that environment. <see cref="WebApplicationFactory{TEntryPoint}"/>
/// defaults to the Development environment, same as <see cref="HealthCheckTests"/>.
/// </summary>
public sealed class OpenApiTests : IClassFixture<OrderCoreApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiTests(OrderCoreApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"openapi\"");
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task OpenApi_document_declares_the_bearer_scheme_and_marks_protected_operations()
    {
        var body = await _factory.CreateClient().GetStringAsync("/openapi/v1.json");

        using var document = System.Text.Json.JsonDocument.Parse(body);
        var root = document.RootElement;
        root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();

        var checkout = root.GetProperty("paths").GetProperty("/api/orders/checkout").GetProperty("post");
        checkout.TryGetProperty("security", out _).Should().BeTrue();
        checkout.GetProperty("responses").TryGetProperty("401", out _).Should().BeTrue();

        var quote = root.GetProperty("paths").GetProperty("/api/orders/cart/quote").GetProperty("post");
        quote.TryGetProperty("security", out _).Should().BeFalse("the cart quote is public");
    }
}
