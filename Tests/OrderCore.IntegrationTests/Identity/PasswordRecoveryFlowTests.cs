using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrderCore.IntegrationTests.Identity;

/// <summary>
/// Forgot, reset and change password through the real API, database and
/// SMTP (Mailpit): the reset e-mail arrives in Portuguese with a link to the
/// storefront, its token sets a new password once, and every old session
/// ends (Docs/specs/identity/password-recovery.md, items 2-4).
/// </summary>
public sealed partial class PasswordRecoveryFlowTests : IClassFixture<ApiDatabase>
{
    private const string NewPassword = "n3w-buyer-pass";

    private readonly ApiDatabase _database;

    public PasswordRecoveryFlowTests(ApiDatabase database)
    {
        _database = database;
    }

    private static async Task<JsonElement> SignUpAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-up", new { name = "Jane Doe", email, password = ApiDatabase.CustomerPassword });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(ApiDatabase.Json);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/sign-in", new { email, password });

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, JsonElement tokens) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() });

    private static async Task<string> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    [Fact]
    public async Task A_forgotten_password_is_reset_through_the_emailed_link_and_old_sessions_end()
    {
        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();
        var email = TestMailpit.NewAddress("jane");
        var session = await SignUpAsync(client, email);

        (await client.PostAsJsonAsync("/api/auth/password/forgot", new { email })).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var message = await TestMailpit.WaitForMessageAsync(email);
        message.Subject.Should().Be("Redefinição de senha");
        message.Html.Should().Contain("Olá, Jane Doe.").And.Contain("O link vale por 30 minutos");
        var link = ResetLink().Match(message.Text);
        link.Success.Should().BeTrue("the e-mail links to the storefront's reset page");
        var token = Uri.UnescapeDataString(link.Groups["token"].Value);

        var reset = await client.PostAsJsonAsync("/api/auth/password/reset", new { token, newPassword = NewPassword });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SignInAsync(client, email, ApiDatabase.CustomerPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SignInAsync(client, email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await RefreshAsync(client, session)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the session from before the reset ended");

        var again = await client.PostAsJsonAsync("/api/auth/password/reset", new { token, newPassword = "an0ther-pass" });
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(again)).Should().Be("invalid_or_expired_token");
    }

    [Fact]
    public async Task An_address_without_an_account_gets_the_same_answer_and_no_email()
    {
        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();
        var stranger = TestMailpit.NewAddress("stranger");
        var known = TestMailpit.NewAddress("known");
        await SignUpAsync(client, known);

        var forStranger = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = stranger });
        var forKnown = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = known });

        forStranger.StatusCode.Should().Be(HttpStatusCode.Accepted);
        forKnown.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await forStranger.Content.ReadAsStringAsync()).Should().Be(await forKnown.Content.ReadAsStringAsync());
        await TestMailpit.WaitForMessageAsync(known);
        (await TestMailpit.CountAsync(stranger)).Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_token_is_refused()
    {
        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/password/reset", new { token = "not-a-token", newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(response)).Should().Be("invalid_or_expired_token");
    }

    [Fact]
    public async Task Changing_the_password_keeps_this_session_and_ends_the_others()
    {
        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();
        var email = TestMailpit.NewAddress("changer");
        var current = await SignUpAsync(client, email);
        var other = await (await SignInAsync(client, email, ApiDatabase.CustomerPassword)).Content.ReadFromJsonAsync<JsonElement>(ApiDatabase.Json);
        ApiDatabase.UseAccessToken(client, current);

        var wrong = await client.PostAsJsonAsync(
            "/api/auth/password/change", new { currentPassword = "wrong-pass1", newPassword = NewPassword });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(wrong)).Should().Be("invalid_current_password");

        var change = await client.PostAsJsonAsync("/api/auth/password/change", new
        {
            currentPassword = ApiDatabase.CustomerPassword,
            newPassword = NewPassword,
            refreshToken = current.GetProperty("refreshToken").GetString(),
        });

        change.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RefreshAsync(client, current)).StatusCode.Should().Be(HttpStatusCode.OK, "this session stays");
        (await RefreshAsync(client, other)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the other session ended");
        (await SignInAsync(client, email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_the_password_needs_a_signed_in_user()
    {
        await using var factory = _database.CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/password/change", new { currentPassword = ApiDatabase.CustomerPassword, newPassword = NewPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [GeneratedRegex(@"http://localhost:3000/redefinir-senha\?token=(?<token>[^\s]+)")]
    private static partial Regex ResetLink();
}
