using FluentAssertions;
using OrderCore.Api.Modules.Identity.Application.Contracts;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Domain.Policies;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Identity;

/// <summary>
/// Forgot, reset and change password (Docs/specs/identity/password-recovery.md,
/// items 2-4), against in-memory fakes: single-use tokens that expire and
/// replace each other, sessions ending, lockout cleared — and the same answer
/// whether an address has an account or not.
/// </summary>
public sealed class PasswordRecoveryTests
{
    private const string Password = "s3cret-pass";
    private const string NewPassword = "n3w-secret-pass";

    private static readonly LockoutPolicy Lockout = new(MaxFailedAttempts: 3, Duration: TimeSpan.FromMinutes(15));

    private readonly FakeUserAccountRepository _accounts = new();
    private readonly FakeCustomerRegistry _customers = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeRefreshTokenGenerator _tokens = new();
    private readonly FakeAccessTokenIssuer _accessTokens = new();
    private readonly FakeAuditLogService _auditLog = new();
    private readonly FakeAccountEmails _emails = new();
    private readonly FakeTimeProvider _clock = new();

    private RequestPasswordResetUseCase Forgot() =>
        new(_accounts, _tokens, _customers, _emails, _auditLog, TestMetrics.Identity, _clock);

    private ResetPasswordUseCase Reset() => new(_accounts, _hasher, _tokens, _auditLog, TestMetrics.Identity, _clock);

    private ChangePasswordUseCase Change() => new(_accounts, _hasher, _tokens, Lockout, _auditLog, TestMetrics.Identity, _clock);

    private SignInUseCase SignIn() =>
        new(_accounts, _hasher, _tokens, _accessTokens, _customers, _clock, Lockout, _auditLog, TestMetrics.Identity);

    private async Task<AuthTokens> SignUpJaneAsync() =>
        await new SignUpCustomerUseCase(_accounts, _customers, _hasher, _tokens, _accessTokens, _auditLog, _clock)
            .ExecuteAsync(new SignUpCommand("Jane Doe", "jane@example.com", Password, Phone: null), CancellationToken.None);

    private UserAccount Jane => _accounts.Accounts.Single();

    [Fact]
    public async Task Forgot_password_emails_a_30_minute_link_to_an_existing_account()
    {
        await SignUpJaneAsync();

        await Forgot().ExecuteAsync("  JANE@example.com ", CancellationToken.None);

        var email = _emails.PasswordResets.Should().ContainSingle().Subject;
        email.Email.Should().Be("jane@example.com");
        email.Name.Should().Be("Jane Doe");
        email.ValidFor.Should().Be(TimeSpan.FromMinutes(30));
        var token = Jane.Tokens.Should().ContainSingle().Subject;
        token.Purpose.Should().Be(AccountTokenPurpose.PasswordReset);
        token.TokenHash.Should().Be(_tokens.Hash(email.Token), "only the hash is kept");
        token.ExpiresAt.Should().Be(_clock.GetUtcNow().AddMinutes(30));
        _auditLog.Actions.Should().Contain("PasswordResetRequested");
    }

    [Theory]
    [InlineData("nobody@example.com")]
    [InlineData("")]
    public async Task Forgot_password_answers_the_same_for_an_address_without_an_account(string email)
    {
        await SignUpJaneAsync();

        var act = () => Forgot().ExecuteAsync(email, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emails.PasswordResets.Should().BeEmpty();
        Jane.Tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Asking_again_replaces_the_previous_link()
    {
        await SignUpJaneAsync();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        var (first, second) = (_emails.PasswordResets[0].Token, _emails.PasswordResets[1].Token);

        var withFirst = () => Reset().ExecuteAsync(first, NewPassword, CancellationToken.None);

        await withFirst.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
        await Reset().ExecuteAsync(second, NewPassword, CancellationToken.None);
        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
    }

    [Fact]
    public async Task Resetting_sets_the_password_ends_every_session_and_clears_the_lockout()
    {
        await SignUpJaneAsync();
        await SignIn().ExecuteAsync(new SignInCommand("jane@example.com", Password), CancellationToken.None);
        for (var i = 0; i < Lockout.MaxFailedAttempts; i++)
        {
            await FluentActions.Awaiting(() => SignIn().ExecuteAsync(new SignInCommand("jane@example.com", "wrong-pass1"), CancellationToken.None))
                .Should().ThrowAsync<Exception>();
        }

        Jane.IsLockedOut(_clock.GetUtcNow()).Should().BeTrue();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);

        await Reset().ExecuteAsync(_emails.PasswordResets.Single().Token, NewPassword, CancellationToken.None);

        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
        Jane.Sessions.Should().HaveCount(2).And.OnlyContain(s => s.RevokedAt != null);
        Jane.IsLockedOut(_clock.GetUtcNow()).Should().BeFalse();
        Jane.FailedSignInCount.Should().Be(0);
        _auditLog.Actions.Should().Contain("PasswordReset");
        await SignIn().ExecuteAsync(new SignInCommand("jane@example.com", NewPassword), CancellationToken.None);
    }

    [Fact]
    public async Task A_reset_link_works_once()
    {
        await SignUpJaneAsync();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        var token = _emails.PasswordResets.Single().Token;
        await Reset().ExecuteAsync(token, NewPassword, CancellationToken.None);

        var again = () => Reset().ExecuteAsync(token, "an0ther-pass", CancellationToken.None);

        await again.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
    }

    [Fact]
    public async Task A_reset_link_expires_after_30_minutes()
    {
        await SignUpJaneAsync();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(30));

        var late = () => Reset().ExecuteAsync(_emails.PasswordResets.Single().Token, NewPassword, CancellationToken.None);

        await late.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + Password);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("")]
    public async Task An_unknown_token_is_refused(string token)
    {
        var act = () => Reset().ExecuteAsync(token, NewPassword, CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
    }

    [Fact]
    public async Task A_weak_new_password_is_refused_without_spending_the_token()
    {
        await SignUpJaneAsync();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        var token = _emails.PasswordResets.Single().Token;

        var weak = () => Reset().ExecuteAsync(token, "short", CancellationToken.None);

        await weak.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "weak_password");
        await Reset().ExecuteAsync(token, NewPassword, CancellationToken.None);
    }

    [Fact]
    public async Task A_deactivated_account_gets_no_link_and_its_old_link_stops_working()
    {
        await SignUpJaneAsync();
        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        Jane.Deactivate(_clock.GetUtcNow());

        await Forgot().ExecuteAsync("jane@example.com", CancellationToken.None);
        var reset = () => Reset().ExecuteAsync(_emails.PasswordResets.Single().Token, NewPassword, CancellationToken.None);

        _emails.PasswordResets.Should().HaveCount(1);
        await reset.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_or_expired_token");
    }

    [Fact]
    public async Task An_admin_can_reset_their_password_too()
    {
        await _accounts.AddAsync(UserAccount.CreateAdmin("admin@example.com", _hasher.Hash(Password), _clock.GetUtcNow()), CancellationToken.None);

        await Forgot().ExecuteAsync("admin@example.com", CancellationToken.None);

        _emails.PasswordResets.Should().ContainSingle().Which.Name.Should().BeNull();
    }

    [Fact]
    public async Task Changing_the_password_keeps_the_current_session_and_ends_the_others()
    {
        var current = await SignUpJaneAsync();
        var other = await SignIn().ExecuteAsync(new SignInCommand("jane@example.com", Password), CancellationToken.None);

        await Change().ExecuteAsync(Jane.Id, Password, NewPassword, current.RefreshToken, CancellationToken.None);

        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + NewPassword);
        Jane.Sessions.Single(s => s.TokenHash == _tokens.Hash(current.RefreshToken)).RevokedAt.Should().BeNull();
        Jane.Sessions.Single(s => s.TokenHash == _tokens.Hash(other.RefreshToken)).RevokedAt.Should().NotBeNull();
        _auditLog.Actions.Should().Contain("PasswordChanged");
    }

    [Fact]
    public async Task Changing_without_a_refresh_token_ends_every_session()
    {
        await SignUpJaneAsync();

        await Change().ExecuteAsync(Jane.Id, Password, NewPassword, currentRefreshToken: null, CancellationToken.None);

        Jane.Sessions.Should().OnlyContain(s => s.RevokedAt != null);
    }

    [Fact]
    public async Task A_wrong_current_password_is_refused_and_counts_toward_the_lockout()
    {
        await SignUpJaneAsync();

        for (var i = 0; i < Lockout.MaxFailedAttempts; i++)
        {
            var wrong = () => Change().ExecuteAsync(Jane.Id, "wrong-pass1", NewPassword, null, CancellationToken.None);
            await wrong.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_current_password");
        }

        Jane.IsLockedOut(_clock.GetUtcNow()).Should().BeTrue();
        var whileLocked = () => Change().ExecuteAsync(Jane.Id, Password, NewPassword, null, CancellationToken.None);
        await whileLocked.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "invalid_current_password");
        Jane.PasswordHash.Should().Be(FakePasswordHasher.Prefix + Password);
    }

    private sealed class FakeAccountEmails : IAccountEmails
    {
        public List<(string Email, string? Name, string Token, TimeSpan ValidFor)> PasswordResets { get; } = [];

        public Task SendPasswordResetAsync(string email, string? name, string token, TimeSpan validFor, CancellationToken cancellationToken)
        {
            PasswordResets.Add((email, name, token, validFor));
            return Task.CompletedTask;
        }
    }
}
