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
public sealed class OpenApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiTests(WebApplicationFactory<Program> factory)
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
}
