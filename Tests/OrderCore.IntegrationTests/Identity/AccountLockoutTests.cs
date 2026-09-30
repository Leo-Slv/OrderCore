using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static OrderCore.IntegrationTests.ApiDatabase;

namespace OrderCore.IntegrationTests.Identity;

/// <summary>
/// Account lockout through the real host and database, with a short policy
/// (3 wrong passwords, 2 seconds): the account locks, a locked account
/// answers the right password exactly like a wrong one, and it opens again
/// when the lock runs out — the count kept in the database between requests.
/// </summary>
public sealed class AccountLockoutTests : IClassFixture<ApiDatabase>
{
    private readonly ApiDatabase _database;

    public AccountLockoutTests(ApiDatabase database)
    {
        _database = database;
    }

    private WebApplicationFactory<Program> Factory() =>
        _database.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:Lockout:MaxFailedAttempts", "3");
            builder.UseSetting("Identity:Lockout:Duration", "00:00:02");
        });

    private static async Task<(HttpStatusCode Status, string? Code)> SignInAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new { email, password });
        var code = response.IsSuccessStatusCode
            ? null
            : (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString();
        return (response.StatusCode, code);
    }

    [Fact]
    public async Task Wrong_passwords_lock_the_account_until_the_lock_runs_out()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var email = $"locked-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/auth/sign-up", new { name = "Jane Doe", email, password = CustomerPassword }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        for (var i = 0; i < 3; i++)
        {
            (await SignInAsync(client, email, "wrong-pass-1")).Should().Be((HttpStatusCode.Unauthorized, "invalid_credentials"));
        }

        (await SignInAsync(client, email, CustomerPassword))
            .Should().Be((HttpStatusCode.Unauthorized, "invalid_credentials"), "a locked account answers like a wrong password");

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        (await SignInAsync(client, email, CustomerPassword)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_email_answers_like_a_locked_account()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();

        (await SignInAsync(client, $"nobody-{Guid.NewGuid():N}@example.com", CustomerPassword))
            .Should().Be((HttpStatusCode.Unauthorized, "invalid_credentials"));
    }
}
