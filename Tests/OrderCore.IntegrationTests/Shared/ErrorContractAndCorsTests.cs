using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Customers.Application.Contracts;
using OrderCore.Api.Modules.Customers.Domain.Entities;
using Xunit;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// Exercises the real HTTP pipeline (routing, <c>UseExceptionHandler</c>,
/// CORS) without a database: <see cref="ICustomerRepository"/> is swapped
/// for an empty in-memory stub, so a lookup fails the same way it would
/// against PostgreSQL with no matching row.
/// </summary>
public sealed class ErrorContractAndCorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AllowedOrigin = "http://localhost:3000";

    private readonly WebApplicationFactory<Program> _factory;

    public ErrorContractAndCorsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ICustomerRepository, EmptyCustomerRepository>()));
    }

    [Fact]
    public async Task Unknown_resource_returns_404_problem_details_with_error_code()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/customers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("code").GetString().Should().Be("customer_not_found");
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details_with_a_default_code()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("not_found");
    }

    [Fact]
    public async Task Malformed_request_body_returns_400_problem_details_with_validation_error()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/orders/cart/quote", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("validation_error");
    }

    [Fact]
    public async Task Preflight_from_configured_origin_is_allowed()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(Preflight(AllowedOrigin));

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task Preflight_from_unknown_origin_is_not_allowed()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/catalog/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    private sealed class EmptyCustomerRepository : ICustomerRepository
    {
        public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) => Task.FromResult<Customer?>(null);

        public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<Customer?>(null);

        public Task AddAsync(Customer customer, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
