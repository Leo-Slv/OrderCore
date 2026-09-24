using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;

namespace OrderCore.IntegrationTests;

/// <summary>
/// The real API host with the setting every test host needs: a JWT signing
/// key (the API refuses to start without one). Tests needing more (a
/// database, a seed admin, a fake payment mode) add it with
/// <c>WithWebHostBuilder</c>, which keeps this configuration.
/// </summary>
public class OrderCoreApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-with-enough-bytes";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SigningKey", SigningKey);
    }
}

/// <summary>
/// Clients signed in as made-up accounts, with tokens issued by the API's
/// own issuer and key. The accounts don't exist in any database, which is
/// enough for endpoints that only look at the token's claims.
/// </summary>
public static class SignedInClients
{
    public static HttpClient CreateClientSignedInAs(this WebApplicationFactory<Program> factory, UserAccount account)
    {
        var issuer = new JwtAccessTokenIssuer(factory.Services.GetRequiredService<IOptions<JwtOptions>>());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issuer.Issue(account, DateTimeOffset.UtcNow).Value);
        return client;
    }

    public static HttpClient CreateAdminClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClientSignedInAs(UserAccount.CreateAdmin("admin@example.com", "not-a-real-hash", DateTimeOffset.UtcNow));

    public static HttpClient CreateCustomerClient(this WebApplicationFactory<Program> factory, Guid? customerId = null)
    {
        var account = UserAccount.CreateCustomer("jane@example.com", "not-a-real-hash", DateTimeOffset.UtcNow);
        account.LinkCustomer(customerId ?? Guid.NewGuid());
        return factory.CreateClientSignedInAs(account);
    }
}
