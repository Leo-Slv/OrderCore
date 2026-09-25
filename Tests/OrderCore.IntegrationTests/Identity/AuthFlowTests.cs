using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Identity;

/// <summary>
/// Sign-up, sign-in, refresh and sign-out through the real host and
/// PostgreSQL, including the parts that only show up end to end: the
/// issued access token actually opens the protected endpoints, and a
/// replayed refresh token ends the whole session. One database for the
/// class; every test uses its own e-mail.
/// </summary>
public sealed class AuthFlowTests : IClassFixture<ApiDatabase>, IAsyncLifetime
{
    private readonly ApiDatabase _database;
    private WebApplicationFactory<Program> _factory = null!;

    public AuthFlowTests(ApiDatabase database)
    {
        _database = database;
    }

    public Task InitializeAsync()
    {
        _factory = _database.CreateFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static string NewEmail() => $"flow-{Guid.NewGuid():N}@example.com";

    private async Task<HttpResponseMessage> PostAsync(string url, object body, string? accessToken = null)
    {
        var client = _factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.PostAsJsonAsync(url, body);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("code").GetString();

    private async Task<HttpStatusCode> GetMyProfileStatusAsync(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (await client.GetAsync("/api/customers/me")).StatusCode;
    }

    [Fact]
    public async Task Sign_up_returns_tokens_that_open_the_customers_own_profile()
    {
        var email = NewEmail();

        var signUp = await PostAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = CustomerPassword });

        signUp.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = await ReadAsync(signUp);
        tokens.GetProperty("role").GetString().Should().Be("Customer");

        var client = _factory.CreateClient();
        UseAccessToken(client, tokens);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/customers/me", Json);
        profile.GetProperty("id").GetGuid().Should().Be(tokens.GetProperty("customerId").GetGuid());
        profile.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Sign_in_accepts_the_right_password_whatever_the_email_case()
    {
        var email = NewEmail();
        await PostAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = CustomerPassword });

        var signIn = await PostAsync("/api/auth/sign-in", new { email = email.ToUpperInvariant(), password = CustomerPassword });

        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetMyProfileStatusAsync((await ReadAsync(signIn)).GetProperty("accessToken").GetString()!))
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sign_in_fails_the_same_way_for_a_wrong_password_and_an_unknown_email()
    {
        var email = NewEmail();
        await PostAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = CustomerPassword });

        var wrongPassword = await PostAsync("/api/auth/sign-in", new { email, password = "wrong-pass-1" });
        var unknownEmail = await PostAsync("/api/auth/sign-in", new { email = NewEmail(), password = CustomerPassword });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOfAsync(wrongPassword)).Should().Be("invalid_credentials");
        (await CodeOfAsync(unknownEmail)).Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Sign_up_rejects_a_weak_password_and_a_taken_email()
    {
        var email = NewEmail();

        var weak = await PostAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = "short" });
        await PostAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = CustomerPassword });
        var taken = await PostAsync("/api/auth/sign-up", new { name = "Jane Again", email, password = CustomerPassword });

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOfAsync(weak)).Should().Be("weak_password");
        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOfAsync(taken)).Should().Be("email_already_registered");
    }

    [Fact]
    public async Task Refresh_rotates_the_tokens_and_a_replayed_refresh_token_ends_the_session()
    {
        var signUp = await ReadAsync(await PostAsync(
            "/api/auth/sign-up", new { name = "Jane Doe", email = NewEmail(), password = CustomerPassword }));
        var firstRefreshToken = signUp.GetProperty("refreshToken").GetString();

        var refresh = await PostAsync("/api/auth/refresh", new { refreshToken = firstRefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await ReadAsync(refresh);
        rotated.GetProperty("refreshToken").GetString().Should().NotBe(firstRefreshToken);
        (await GetMyProfileStatusAsync(rotated.GetProperty("accessToken").GetString()!)).Should().Be(HttpStatusCode.OK);

        // Someone replays the first (already rotated) refresh token...
        var replay = await PostAsync("/api/auth/refresh", new { refreshToken = firstRefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await CodeOfAsync(replay)).Should().Be("invalid_refresh_token");

        // ...which revokes the session, so the legitimate new token dies too.
        var afterReplay = await PostAsync("/api/auth/refresh", new { refreshToken = rotated.GetProperty("refreshToken").GetString() });
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sign_out_ends_the_session_and_requires_being_signed_in()
    {
        var tokens = await ReadAsync(await PostAsync(
            "/api/auth/sign-up", new { name = "Jane Doe", email = NewEmail(), password = CustomerPassword }));
        var refreshToken = tokens.GetProperty("refreshToken").GetString();

        var anonymousSignOut = await PostAsync("/api/auth/sign-out", new { refreshToken });
        anonymousSignOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var signOut = await PostAsync("/api/auth/sign-out", new { refreshToken }, tokens.GetProperty("accessToken").GetString());
        signOut.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refresh = await PostAsync("/api/auth/refresh", new { refreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_seeded_admin_signs_in_as_an_admin_and_reaches_admin_endpoints()
    {
        var admin = await SignInAsAdminAsync(_factory);

        (await admin.GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/customers/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "an admin is not a customer");
    }

    [Fact]
    public async Task A_tampered_access_token_is_rejected()
    {
        var tokens = await ReadAsync(await PostAsync(
            "/api/auth/sign-up", new { name = "Jane Doe", email = NewEmail(), password = CustomerPassword }));
        var token = tokens.GetProperty("accessToken").GetString()!;
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        (await GetMyProfileStatusAsync(tampered)).Should().Be(HttpStatusCode.Unauthorized);
    }
}
