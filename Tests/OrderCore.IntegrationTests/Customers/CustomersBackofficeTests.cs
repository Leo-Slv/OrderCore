using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Customers;

/// <summary>
/// The Customers backoffice through the real host: the list searches name
/// and e-mail in PostgreSQL, and deactivating a customer locks them out of
/// sign-in and refresh until an admin reactivates them.
/// </summary>
public sealed class CustomersBackofficeTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public CustomersBackofficeTests(ApiDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task An_admin_finds_a_customer_by_part_of_their_name_or_email()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await SignUpCustomerAsync(factory, name: $"Marina {marker} Costa");

        var byName = await admin.GetFromJsonAsync<JsonElement>($"/api/customers?searchTerm={marker.ToUpperInvariant()}", Json);
        var customer = byName.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        customer.GetProperty("name").GetString().Should().Be($"Marina {marker} Costa");
        customer.GetProperty("active").GetBoolean().Should().BeTrue();

        var email = customer.GetProperty("email").GetString()!;
        var byEmail = await admin.GetFromJsonAsync<JsonElement>($"/api/customers?searchTerm={email[..12]}", Json);
        byEmail.GetProperty("items").EnumerateArray().Should().Contain(c => c.GetProperty("email").GetString() == email);
    }

    [Fact]
    public async Task A_deactivated_customer_cannot_sign_in_or_refresh_until_reactivated()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);
        var (customer, tokens) = await SignUpCustomerAsync(factory);
        var customerId = tokens.GetProperty("customerId").GetGuid();
        var me = await customer.GetFromJsonAsync<JsonElement>("/api/customers/me", Json);
        var email = me.GetProperty("email").GetString();
        var anonymous = factory.CreateClient();

        var deactivated = await admin.PostAsync($"/api/customers/{customerId}/deactivate", null);
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deactivated.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("active").GetBoolean().Should().BeFalse();

        var signIn = await anonymous.PostAsJsonAsync("/api/auth/sign-in", new { email, password = CustomerPassword });
        signIn.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await signIn.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("account_inactive");

        var wrongPassword = await anonymous.PostAsJsonAsync("/api/auth/sign-in", new { email, password = "wrong-pass-9" });
        (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("invalid_credentials");

        var refreshToken = tokens.GetProperty("refreshToken").GetString();
        var refresh = await anonymous.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await admin.PostAsync($"/api/customers/{customerId}/reactivate", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await anonymous.PostAsJsonAsync("/api/auth/sign-in", new { email, password = CustomerPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivating_an_unknown_customer_is_not_found()
    {
        await using var factory = _database.CreateFactory();
        var admin = await SignInAsAdminAsync(factory);

        var response = await admin.PostAsync($"/api/customers/{Guid.NewGuid()}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
