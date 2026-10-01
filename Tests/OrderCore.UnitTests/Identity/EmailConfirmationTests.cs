using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Infrastructure.Security;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Identity;

/// <summary>
/// E-mail confirmation (Docs/specs/identity/password-recovery.md, item 5):
/// sign-up e-mails a 24-hour link, the link confirms once, a new one replaces
/// it, and the access token says whether the address is confirmed.
/// </summary>
public sealed class EmailConfirmationTests
{
    private const string Password = "s3cret-pass";

    private readonly FakeUserAccountRepository _accounts = new();
    private readonly FakeCustomerRegistry _customers = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeRefreshTokenGenerator _tokens = new();
    private readonly FakeAccessTokenIssuer _accessTokens = new();
    private readonly FakeAuditLogService _auditLog = new();
    private readonly FakeAccountEmails _emails = new();
    private readonly FakeTimeProvider _clock = new();

    private RequestEmailConfirmationUseCase Request() => new(_accounts, _tokens, _customers, _emails, _clock);

    private ConfirmEmailUseCase Confirm() => new(_accounts, _tokens, _auditLog, _clock);

    private Task<AuthTokens> SignUpJaneAsync() =>
        new SignUpCustomerUseCase(
                _accounts, _customers, _hasher, _tokens, _accessTokens, _auditLog, _clock, Request(), NullLogger<SignUpCustomerUseCase>.Instance)
            .ExecuteAsync(new SignUpCommand("Jane Doe", "jane@example.com", Password, Phone: null), CancellationToken.None);

    private UserAccount Jane => _accounts.Accounts.Single();

    [Fact]
    public async Task Sign_up_starts_unconfirmed_and_emails_a_24_hour_link()
    {
        await SignUpJaneAsync();

        Jane.EmailConfirmed.Should().BeFalse();
        var email = _emails.EmailConfirmations.Should().ContainSingle().Subject;
        email.Email.Should().Be("jane@example.com");
        email.Name.Should().Be("Jane Doe");
        email.ValidFor.Should().Be(TimeSpan.FromHours(24));
        Jane.Tokens.Single().ExpiresAt.Should().Be(_clock.GetUtcNow().AddHours(24));
    }

    [Fact]
    public async Task Sign_up_succeeds_even_when_the_link_cant_be_queued()
    {
        _emails.FailWith = new InvalidOperationException("database down");

        var tokens = await SignUpJaneAsync();

        tokens.CustomerId.Should().NotBeNull();
        Jane.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task The_link_confirms_the_address_once_and_opening_it_again_is_harmless()
    {
        await SignUpJaneAsync();
        var token = _emails.EmailConfirmations.Single().Token;

        await Confirm().ExecuteAsync(token, CancellationToken.None);
        await Confirm().ExecuteAsync(token, CancellationToken.None);

        Jane.EmailConfirmedAt.Should().Be(_clock.GetUtcNow());
        _auditLog.Actions.Count(a => a == "EmailConfirmed").Should().Be(1);
    }

    [Fact]
    public async Task The_link_expires_after_24_hours()
    {
        await SignUpJaneAsync();
        _clock.Advance(TimeSpan.FromHours(24));

        var late = () => Confirm().ExecuteAsync(_emails.EmailConfirmations.Single().Token, CancellationToken.None);

        await late.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
        Jane.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task A_new_link_replaces_the_old_one_and_a_confirmed_address_gets_none()
    {
        await SignUpJaneAsync();
        var first = _emails.EmailConfirmations.Single().Token;

        await Request().ExecuteAsync(Jane.Id, CancellationToken.None);
        var withFirst = () => Confirm().ExecuteAsync(first, CancellationToken.None);

        await withFirst.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
        await Confirm().ExecuteAsync(_emails.EmailConfirmations[1].Token, CancellationToken.None);
        var again = () => Request().ExecuteAsync(Jane.Id, CancellationToken.None);
        await again.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "email_already_confirmed");
    }

    [Fact]
    public void An_admin_is_created_confirmed()
    {
        UserAccount.CreateAdmin("admin@example.com", "hash", _clock.GetUtcNow()).EmailConfirmed.Should().BeTrue();
        UserAccount.CreateCustomer("jane@example.com", "hash", _clock.GetUtcNow()).EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public void The_access_token_says_whether_the_address_is_confirmed()
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(new JwtOptions { SigningKey = new string('k', 48) }));
        var customer = UserAccount.CreateCustomer("jane@example.com", "hash", _clock.GetUtcNow());
        var admin = UserAccount.CreateAdmin("admin@example.com", "hash", _clock.GetUtcNow());

        Claim(issuer.Issue(customer, _clock.GetUtcNow()).Value).Should().Be("false");
        Claim(issuer.Issue(admin, _clock.GetUtcNow()).Value).Should().Be("true");
    }

    private static string Claim(string token) =>
        new JsonWebTokenHandler().ReadJsonWebToken(token).GetClaim("email_confirmed").Value;
}
