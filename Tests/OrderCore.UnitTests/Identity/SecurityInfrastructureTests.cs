using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;
using OrderCore.Api.Shared.Presentation.Authentication;
using Xunit;

namespace OrderCore.UnitTests.Identity;

/// <summary>The real hasher, token issuer and refresh-token generator (no fakes).</summary>
public sealed class SecurityInfrastructureTests
{
    private static readonly JwtOptions Settings = new()
    {
        Issuer = "OrderCore",
        Audience = "OrderCore",
        SigningKey = "unit-test-signing-key-that-is-long-enough",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 14,
    };

    [Fact]
    public void Password_hasher_verifies_the_right_password_only()
    {
        var hasher = new AspNetPasswordHasher();

        var hash = hasher.Hash("s3cret-pass");

        hash.Should().NotContain("s3cret-pass");
        hasher.Verify(hash, "s3cret-pass").Should().Be(PasswordCheck.Succeeded);
        hasher.Verify(hash, "wrong-pass1").Should().Be(PasswordCheck.Failed);
        hasher.Hash("s3cret-pass").Should().NotBe(hash, "every hash is salted");
    }

    [Fact]
    public async Task Access_token_is_a_signed_jwt_with_the_accounts_claims()
    {
        var now = DateTimeOffset.UtcNow;
        var account = UserAccount.CreateCustomer("jane@example.com", "hash", now);
        var customerId = Guid.NewGuid();
        account.LinkCustomer(customerId);

        var token = new JwtAccessTokenIssuer(Options.Create(Settings)).Issue(account, now);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            IssuerSigningKey = JwtAccessTokenIssuer.SigningKey(Settings),
        });

        result.IsValid.Should().BeTrue();
        result.Claims[OrderCoreClaimTypes.UserId].Should().Be(account.Id.ToString());
        result.Claims[OrderCoreClaimTypes.Role].Should().Be("Customer");
        result.Claims[OrderCoreClaimTypes.CustomerId].Should().Be(customerId.ToString());
        token.ExpiresAt.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Access_token_signed_with_another_key_is_rejected()
    {
        var account = UserAccount.CreateAdmin("admin@example.com", "hash", DateTimeOffset.UtcNow);
        var token = new JwtAccessTokenIssuer(Options.Create(Settings)).Issue(account, DateTimeOffset.UtcNow);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Settings.Issuer,
            ValidAudience = Settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey("a-completely-different-signing-key!!"u8.ToArray()),
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Refresh_tokens_are_random_and_stored_only_as_a_hash()
    {
        var generator = new RandomRefreshTokenGenerator(Options.Create(Settings));

        var first = generator.Generate();
        var second = generator.Generate();

        first.Token.Should().NotBe(second.Token);
        first.Hash.Should().NotBe(first.Token).And.HaveLength(64);
        generator.Hash(first.Token).Should().Be(first.Hash);
        generator.Lifetime.Should().Be(TimeSpan.FromDays(14));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("too-short", false)]
    [InlineData("exactly-thirty-two-bytes-long!!!", true)]
    public void Signing_key_must_be_at_least_32_bytes(string key, bool valid)
    {
        new JwtOptions { SigningKey = key }.HasValidSigningKey.Should().Be(valid);
    }
}
