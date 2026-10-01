using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderCore.Api.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace OrderCore.IntegrationTests.Identity;

/// <summary>
/// E-mail confirmation through the real API, database and SMTP (Mailpit):
/// sign-up e-mails a link; until it is opened the customer can't check out
/// (<c>403 email_not_confirmed</c>); after confirming and refreshing the
/// session they can (Docs/specs/identity/password-recovery.md, item 5,
/// decisions 4, 8 and 9).
/// </summary>
public sealed partial class EmailConfirmationTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public EmailConfirmationTests(ApiDatabase database)
    {
        _database = database;
    }

    private static async Task<(HttpClient Client, JsonElement Tokens)> SignUpAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/sign-up", new { name = "Jane Doe", email, password = ApiDatabase.CustomerPassword });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(ApiDatabase.Json);
        ApiDatabase.UseAccessToken(client, tokens);
        return (client, tokens);
    }

    private static async Task<JsonElement> RefreshAsync(HttpClient client, JsonElement tokens)
    {
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await response.Content.ReadFromJsonAsync<JsonElement>(ApiDatabase.Json);
        ApiDatabase.UseAccessToken(client, refreshed);
        return refreshed;
    }

    private static async Task<string> TokenFromEmailAsync(string email, int expectedMessages = 1)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (await TestMailpit.CountAsync(email) < expectedMessages)
        {
            DateTimeOffset.UtcNow.Should().BeBefore(deadline, "the confirmation e-mail should arrive");
            await Task.Delay(100);
        }

        var message = await TestMailpit.WaitForMessageAsync(email, subject: "Confirme seu e-mail");
        message.Subject.Should().Be("Confirme seu e-mail");
        message.Html.Should().Contain("Olá, Jane Doe.").And.Contain("O link vale por 24 horas");
        var link = ConfirmLink().Match(message.Text);
        link.Success.Should().BeTrue("the e-mail links to the storefront's confirmation page");
        return Uri.UnescapeDataString(link.Groups["token"].Value);
    }

    private async Task<(Guid AddressId, Guid ProductId)> ReadyToBuyAsync(WebApplicationFactory<Program> factory, HttpClient customer)
    {
        var admin = await ApiDatabase.SignInAsAdminAsync(factory);
        var (productId, _) = await ApiDatabase.CreatePublishedProductAsync(admin, "Caneca", 30m);
        await _database.SeedStockAsync(productId, 5);
        return (await ApiDatabase.AddAddressAsync(customer), productId);
    }

    private static async Task<string> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

    [Fact]
    public async Task A_new_customer_checks_out_only_after_confirming_the_emailed_link_and_refreshing()
    {
        await using var factory = _database.CreateFactory();
        var email = TestMailpit.NewAddress("jane");
        var (client, tokens) = await SignUpAsync(factory, email);
        var (addressId, productId) = await ReadyToBuyAsync(factory, client);

        var refused = await ApiDatabase.CheckoutAsync(client, addressId, productId, 1, "unconfirmed-1");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "browsing, addresses and the cart work; checkout doesn't");
        (await CodeOf(refused)).Should().Be("email_not_confirmed");

        var token = await TokenFromEmailAsync(email);
        (await client.PostAsJsonAsync("/api/auth/email/confirm", new { token })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var withOldToken = await ApiDatabase.CheckoutAsync(client, addressId, productId, 1, "unconfirmed-2");
        withOldToken.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the access token from before confirming still says unconfirmed");

        await RefreshAsync(client, tokens);
        (await ApiDatabase.CheckoutAsync(client, addressId, productId, 1, "confirmed-1")).StatusCode.Should().Be(HttpStatusCode.Accepted);

        (await client.PostAsJsonAsync("/api/auth/email/confirm", new { token })).StatusCode
            .Should().Be(HttpStatusCode.NoContent, "opening the link again once confirmed is harmless");
        var resend = await client.PostAsync("/api/auth/email/confirmation", null);
        resend.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeOf(resend)).Should().Be("email_already_confirmed");
    }

    [Fact]
    public async Task Asking_for_a_new_link_replaces_the_old_one()
    {
        await using var factory = _database.CreateFactory();
        var email = TestMailpit.NewAddress("again");
        var (client, _) = await SignUpAsync(factory, email);
        var first = await TokenFromEmailAsync(email);

        (await client.PostAsync("/api/auth/email/confirmation", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var second = await TokenFromEmailAsync(email, expectedMessages: 2);

        second.Should().NotBe(first);
        var withFirst = await client.PostAsJsonAsync("/api/auth/email/confirm", new { token = first });
        withFirst.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeOf(withFirst)).Should().Be("invalid_or_expired_token");
        (await client.PostAsJsonAsync("/api/auth/email/confirm", new { token = second })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_account_from_before_confirmation_existed_checks_out_without_confirming()
    {
        await using var factory = _database.CreateFactory();
        var (client, tokens) = await ApiDatabase.SignUpCustomerAsync(factory, confirmEmail: false);
        var (addressId, productId) = await ReadyToBuyAsync(factory, client);

        // What the AddEmailConfirmation migration does to every existing account.
        await using (var db = new IdentityDbContext(_database.Options<IdentityDbContext>()))
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE user_accounts SET \"EmailConfirmedAt\" = \"CreatedAt\" WHERE \"Id\" = {0}", tokens.GetProperty("userId").GetGuid());
        }

        await RefreshAsync(client, tokens);
        (await ApiDatabase.CheckoutAsync(client, addressId, productId, 1, "legacy-1")).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task The_seeded_admin_is_confirmed()
    {
        await using var factory = _database.CreateFactory();
        await ApiDatabase.SignInAsAdminAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var admin = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().UserAccounts
            .SingleAsync(a => a.NormalizedEmail == ApiDatabase.AdminEmail.ToUpperInvariant());
        admin.EmailConfirmedAt.Should().NotBeNull();
    }

    [GeneratedRegex(@"http://localhost:3000/confirmar-email\?token=(?<token>[^\s]+)")]
    private static partial Regex ConfirmLink();
}
