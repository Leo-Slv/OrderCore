using FluentAssertions;
using OrderCore.Api.Modules.Identity.Application.DTOs;
using OrderCore.Api.Modules.Identity.Application.UseCases;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Shared.Application.Exceptions;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Identity;

/// <summary>Sign-up, sign-in, refresh, sign-out and the admin seed, against in-memory fakes.</summary>
public sealed class AuthenticationUseCaseTests
{
    private const string Password = "s3cret-pass";

    private readonly FakeUserAccountRepository _accounts = new();
    private readonly FakeCustomerRegistry _customers = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeRefreshTokenGenerator _refreshTokens = new();
    private readonly FakeAccessTokenIssuer _accessTokens = new();
    private readonly FakeAuditLogService _auditLog = new();
    private readonly FakeTimeProvider _clock = new();

    private SignUpCustomerUseCase SignUp() => new(_accounts, _customers, _hasher, _refreshTokens, _accessTokens, _auditLog, _clock);

    private SignInUseCase SignIn() => new(_accounts, _hasher, _refreshTokens, _accessTokens, _customers, _clock);

    private RefreshSessionUseCase Refresh() => new(_accounts, _refreshTokens, _accessTokens, _auditLog, _customers, _clock);

    private SignOutUseCase SignOut() => new(_accounts, _refreshTokens, _clock);

    private SeedAdminUseCase SeedAdmin() => new(_accounts, _hasher, _auditLog, _clock);

    private Task<AuthTokens> SignUpJaneAsync() =>
        SignUp().ExecuteAsync(new SignUpCommand("Jane Doe", "jane@example.com", Password, Phone: null), CancellationToken.None);

    [Fact]
    public async Task SignUp_creates_a_linked_customer_account_and_signs_it_in()
    {
        var tokens = await SignUpJaneAsync();

        var account = _accounts.Accounts.Should().ContainSingle().Subject;
        account.PasswordHash.Should().Be(FakePasswordHasher.Prefix + Password);
        account.CustomerId.Should().Be(_customers.Registered.Single().Id);
        tokens.CustomerId.Should().Be(account.CustomerId);
        tokens.Role.Should().Be("Customer");
        tokens.RefreshToken.Should().Be("refresh-1");
        tokens.RefreshTokenExpiresAt.Should().Be(_clock.GetUtcNow().AddDays(14));
        _auditLog.Actions.Should().Contain("UserAccountCreated");
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("1234567890")]
    public async Task SignUp_rejects_a_weak_password(string password)
    {
        var act = () => SignUp().ExecuteAsync(new SignUpCommand("Jane", "jane@example.com", password, null), CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "weak_password");
        _accounts.Accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task SignUp_rejects_an_email_that_already_has_an_account_whatever_its_case()
    {
        await SignUpJaneAsync();

        var act = () => SignUp().ExecuteAsync(new SignUpCommand("Jane", "JANE@example.com", Password, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().Where(e => e.Code == "email_already_registered");
    }

    [Fact]
    public async Task SignUp_removes_the_account_again_when_the_customer_cannot_be_created()
    {
        _customers.FailWith = new ConflictException("email_already_registered", "taken in Customers");

        var act = SignUpJaneAsync;

        await act.Should().ThrowAsync<ConflictException>();
        _accounts.Accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task SignIn_with_the_right_password_starts_a_new_session()
    {
        await SignUpJaneAsync();

        var tokens = await SignIn().ExecuteAsync(new SignInCommand("Jane@Example.com", Password), CancellationToken.None);

        tokens.RefreshToken.Should().Be("refresh-2");
        _accounts.Accounts.Single().Sessions.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("jane@example.com", "wrong-pass1")]
    [InlineData("nobody@example.com", Password)]
    public async Task SignIn_fails_the_same_way_for_a_wrong_password_or_an_unknown_email(string email, string password)
    {
        await SignUpJaneAsync();

        var act = () => SignIn().ExecuteAsync(new SignInCommand(email, password), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_credentials");
    }

    [Fact]
    public async Task SignIn_refuses_a_deactivated_account()
    {
        await SignUpJaneAsync();
        _accounts.Accounts.Single().Deactivate(_clock.GetUtcNow());

        var act = () => SignIn().ExecuteAsync(new SignInCommand("jane@example.com", Password), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_credentials");
    }

    [Fact]
    public async Task SignIn_replaces_an_outdated_password_hash()
    {
        var admin = UserAccount.CreateAdmin("admin@example.com", FakePasswordHasher.OutdatedPrefix + Password, _clock.GetUtcNow());
        await _accounts.AddAsync(admin, CancellationToken.None);

        await SignIn().ExecuteAsync(new SignInCommand("admin@example.com", Password), CancellationToken.None);

        admin.PasswordHash.Should().Be(FakePasswordHasher.Prefix + Password);
    }

    [Fact]
    public async Task Refresh_hands_out_a_new_refresh_token_and_retires_the_old_one()
    {
        var signedUp = await SignUpJaneAsync();

        var refreshed = await Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);

        refreshed.RefreshToken.Should().NotBe(signedUp.RefreshToken);
        refreshed.CustomerId.Should().Be(signedUp.CustomerId);
    }

    [Fact]
    public async Task Refresh_with_a_replayed_token_revokes_the_session_so_the_new_token_stops_working_too()
    {
        var signedUp = await SignUpJaneAsync();
        var refreshed = await Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);

        var replay = () => Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);
        await replay.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_refresh_token");

        var useNewToken = () => Refresh().ExecuteAsync(refreshed.RefreshToken, CancellationToken.None);
        await useNewToken.Should().ThrowAsync<UnauthorizedException>();
        _auditLog.Actions.Should().Contain("RefreshTokenReuseDetected");
    }

    [Fact]
    public async Task Refresh_after_the_session_expired_is_refused()
    {
        var signedUp = await SignUpJaneAsync();
        _clock.Advance(TimeSpan.FromDays(15));

        var act = () => Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_refresh_token");
    }

    [Theory]
    [InlineData("")]
    [InlineData("never-issued")]
    public async Task Refresh_with_a_missing_or_unknown_token_is_refused(string token)
    {
        var act = () => Refresh().ExecuteAsync(token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_refresh_token");
    }

    [Fact]
    public async Task SignOut_ends_the_session()
    {
        var signedUp = await SignUpJaneAsync();

        await SignOut().ExecuteAsync(signedUp.UserId, signedUp.RefreshToken, CancellationToken.None);

        var act = () => Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task SignOut_with_someone_elses_token_changes_nothing()
    {
        var signedUp = await SignUpJaneAsync();

        await SignOut().ExecuteAsync(Guid.NewGuid(), signedUp.RefreshToken, CancellationToken.None);

        var refreshed = await Refresh().ExecuteAsync(signedUp.RefreshToken, CancellationToken.None);
        refreshed.Should().NotBeNull();
    }

    [Fact]
    public async Task SeedAdmin_creates_the_first_admin_only_once()
    {
        var created = await SeedAdmin().ExecuteAsync("admin@example.com", Password, CancellationToken.None);
        var createdAgain = await SeedAdmin().ExecuteAsync("other-admin@example.com", Password, CancellationToken.None);

        created.Should().BeTrue();
        createdAgain.Should().BeFalse();
        _accounts.Accounts.Should().ContainSingle(a => a.Role == UserRole.Admin && a.CustomerId == null);
    }

    [Fact]
    public async Task SeedAdmin_applies_the_password_policy()
    {
        var act = () => SeedAdmin().ExecuteAsync("admin@example.com", "weak", CancellationToken.None);

        await act.Should().ThrowAsync<DomainRuleViolationException>().Where(e => e.Code == "weak_password");
    }

    [Fact]
    public async Task SignIn_refuses_a_customer_an_admin_deactivated_and_accepts_them_once_reactivated()
    {
        await SignUpJaneAsync();
        var customerId = _customers.Registered.Single().Id;
        _customers.Inactive.Add(customerId);

        var act = () => SignIn().ExecuteAsync(new SignInCommand("jane@example.com", Password), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_credentials");

        _customers.Inactive.Remove(customerId);
        var tokens = await SignIn().ExecuteAsync(new SignInCommand("jane@example.com", Password), CancellationToken.None);
        tokens.CustomerId.Should().Be(customerId);
    }

    [Fact]
    public async Task Refresh_refuses_a_customer_an_admin_deactivated_without_revoking_the_session()
    {
        var tokens = await SignUpJaneAsync();
        var customerId = _customers.Registered.Single().Id;
        _customers.Inactive.Add(customerId);

        var act = () => Refresh().ExecuteAsync(tokens.RefreshToken, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>().Where(e => e.Code == "invalid_refresh_token");

        _customers.Inactive.Remove(customerId);
        var refreshed = await Refresh().ExecuteAsync(tokens.RefreshToken, CancellationToken.None);
        refreshed.CustomerId.Should().Be(customerId);
    }
}
