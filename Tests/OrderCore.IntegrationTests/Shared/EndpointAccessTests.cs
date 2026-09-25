using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Shared;

/// <summary>
/// Who can call what, checked over HTTP with real accounts: anonymous, a
/// signed-in customer and the seeded admin against a sample of every
/// access class. <c>EndpointAuthorizationTests</c> (architecture) already
/// makes sure every action is classified; this checks that the
/// classification means what it says once the whole pipeline runs. The
/// sample favors endpoints whose success doesn't depend on existing data,
/// so the status really reflects access.
/// </summary>
public sealed class EndpointAccessTests : IClassFixture<ApiDatabase>, IAsyncLifetime
{
    private readonly ApiDatabase _database;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _customer = null!;
    private HttpClient _admin = null!;

    public EndpointAccessTests(ApiDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        _factory = _database.CreateFactory();
        _anonymous = _factory.CreateClient();
        (_customer, _) = await SignUpCustomerAsync(_factory);
        _admin = await SignInAsAdminAsync(_factory);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    public enum Caller
    {
        Anonymous,
        Customer,
        Admin,
    }

    private HttpClient ClientFor(Caller caller) => caller switch
    {
        Caller.Anonymous => _anonymous,
        Caller.Customer => _customer,
        _ => _admin,
    };

    [Theory]
    // Public: anyone.
    [InlineData(Caller.Anonymous, "GET", "/api/catalog/products", HttpStatusCode.OK)]
    [InlineData(Caller.Anonymous, "GET", "/api/catalog/categories", HttpStatusCode.OK)]
    [InlineData(Caller.Anonymous, "POST", "/api/orders/cart/quote", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/catalog/products", HttpStatusCode.OK)]
    // Customer only.
    [InlineData(Caller.Anonymous, "GET", "/api/customers/me", HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Customer, "GET", "/api/customers/me", HttpStatusCode.OK)]
    [InlineData(Caller.Admin, "GET", "/api/customers/me", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Anonymous, "GET", "/api/orders/me", HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Customer, "GET", "/api/orders/me", HttpStatusCode.OK)]
    [InlineData(Caller.Admin, "GET", "/api/orders/me", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "POST", "/api/orders/checkout", HttpStatusCode.Forbidden)]
    // Admin only.
    [InlineData(Caller.Anonymous, "GET", "/api/audit-logs", HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Customer, "GET", "/api/audit-logs", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/audit-logs", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "POST", "/api/catalog/categories", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "POST", "/api/catalog/categories", HttpStatusCode.Created)]
    [InlineData(Caller.Customer, "GET", "/api/orders/customers/00000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/orders/customers/00000000-0000-0000-0000-000000000001", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/inventory/stock-items/00000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Customer, "GET", "/api/payments/orders/00000000-0000-0000-0000-000000000001", HttpStatusCode.Forbidden)]
    // Backoffice (admin only): the admin/ reads and the new lists.
    [InlineData(Caller.Anonymous, "GET", "/api/admin/orders", HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Customer, "GET", "/api/admin/orders", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/admin/orders", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/admin/dashboard", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/admin/dashboard", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/admin/catalog/products", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/admin/catalog/products", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/payments", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/payments", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/customers", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/customers", HttpStatusCode.OK)]
    [InlineData(Caller.Customer, "GET", "/api/inventory/stock-items", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "GET", "/api/inventory/stock-items", HttpStatusCode.OK)]
    // Backoffice commands: a customer is refused before the order is even looked up.
    [InlineData(Caller.Anonymous, "POST", "/api/orders/00000000-0000-0000-0000-000000000001/ship", HttpStatusCode.Unauthorized)]
    [InlineData(Caller.Customer, "POST", "/api/orders/00000000-0000-0000-0000-000000000001/ship", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Admin, "POST", "/api/orders/00000000-0000-0000-0000-000000000001/ship", HttpStatusCode.NotFound)]
    [InlineData(Caller.Customer, "POST", "/api/orders/00000000-0000-0000-0000-000000000001/cancel", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Customer, "POST", "/api/customers/00000000-0000-0000-0000-000000000001/deactivate", HttpStatusCode.Forbidden)]
    [InlineData(Caller.Customer, "POST", "/api/inventory/stock-items/00000000-0000-0000-0000-000000000001/receive", HttpStatusCode.Forbidden)]
    public async Task Access_matches_the_endpoints_classification(Caller caller, string method, string url, HttpStatusCode expected)
    {
        var client = ClientFor(caller);

        var response = method == "GET"
            ? await client.GetAsync(url)
            : await client.PostAsJsonAsync(url, BodyFor(url));

        response.StatusCode.Should().Be(expected);
    }

    private static object BodyFor(string url) => url switch
    {
        "/api/catalog/categories" => new { name = $"Category {Guid.NewGuid():N}" },
        "/api/orders/cart/quote" => new { items = Array.Empty<object>() },
        _ => new { },
    };
}
