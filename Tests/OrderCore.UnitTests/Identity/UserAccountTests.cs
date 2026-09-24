using FluentAssertions;
using OrderCore.Api.Modules.Identity.Domain.Entities;
using OrderCore.Api.Modules.Identity.Domain.Enums;
using OrderCore.Api.Modules.Identity.Domain.Events;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Identity;

public sealed class UserAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset InTwoWeeks = Now.AddDays(14);

    private static UserAccount Customer() => UserAccount.CreateCustomer(" Jane@Example.com ", "hash", Now);

    [Fact]
    public void CreateCustomer_normalizes_the_email_and_starts_unlinked()
    {
        var account = Customer();

        account.Email.Should().Be("Jane@Example.com");
        account.NormalizedEmail.Should().Be("JANE@EXAMPLE.COM");
        account.Role.Should().Be(UserRole.Customer);
        account.CustomerId.Should().BeNull();
        account.Active.Should().BeTrue();
        account.DomainEvents.Should().ContainSingle(e => e is UserAccountCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Create_rejects_an_invalid_email(string email)
    {
        var act = () => UserAccount.CreateCustomer(email, "hash", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LinkCustomer_links_once()
    {
        var account = Customer();
        var customerId = Guid.NewGuid();

        account.LinkCustomer(customerId);
        var linkAgain = () => account.LinkCustomer(Guid.NewGuid());

        account.CustomerId.Should().Be(customerId);
        linkAgain.Should().Throw<DomainRuleViolationException>().Where(e => e.Code == "customer_already_linked");
    }

    [Fact]
    public void LinkCustomer_is_not_allowed_for_an_admin()
    {
        var admin = UserAccount.CreateAdmin("admin@example.com", "hash", Now);

        var act = () => admin.LinkCustomer(Guid.NewGuid());

        act.Should().Throw<DomainRuleViolationException>().Where(e => e.Code == "not_a_customer_account");
    }

    [Fact]
    public void StartSession_adds_an_active_session_and_records_the_sign_in()
    {
        var account = Customer();

        var session = account.StartSession("h1", InTwoWeeks, Now);

        account.Sessions.Should().ContainSingle();
        session.IsActive(Now).Should().BeTrue();
        account.LastSignedInAt.Should().Be(Now);
    }

    [Fact]
    public void RotateSession_replaces_the_session_with_a_successor_in_the_same_family()
    {
        var account = Customer();
        var first = account.StartSession("h1", InTwoWeeks, Now);

        var (outcome, successor) = account.RotateSession("h1", "h2", InTwoWeeks, Now.AddMinutes(20));

        outcome.Should().Be(SessionRotationOutcome.Rotated);
        successor!.FamilyId.Should().Be(first.FamilyId);
        first.RevokedAt.Should().NotBeNull();
        first.ReplacedBySessionId.Should().Be(successor.Id);
        successor.IsActive(Now.AddMinutes(20)).Should().BeTrue();
    }

    [Fact]
    public void RotateSession_with_an_already_rotated_token_revokes_the_whole_family()
    {
        var account = Customer();
        account.StartSession("h1", InTwoWeeks, Now);
        var (_, successor) = account.RotateSession("h1", "h2", InTwoWeeks, Now.AddMinutes(20));
        account.ClearDomainEvents();

        var (outcome, newSession) = account.RotateSession("h1", "h3", InTwoWeeks, Now.AddMinutes(30));

        outcome.Should().Be(SessionRotationOutcome.Reused);
        newSession.Should().BeNull();
        successor!.RevokedAt.Should().NotBeNull();
        account.Sessions.Should().OnlyContain(s => s.RevokedAt != null);
        account.DomainEvents.Should().ContainSingle(e => e is RefreshTokenReuseDetected);
    }

    [Fact]
    public void RotateSession_reuse_only_revokes_that_family()
    {
        var account = Customer();
        account.StartSession("laptop", InTwoWeeks, Now);
        var phone = account.StartSession("phone", InTwoWeeks, Now);
        account.RotateSession("laptop", "laptop-2", InTwoWeeks, Now.AddMinutes(1));

        account.RotateSession("laptop", "laptop-3", InTwoWeeks, Now.AddMinutes(2));

        phone.IsActive(Now.AddMinutes(2)).Should().BeTrue();
    }

    [Fact]
    public void RotateSession_of_an_expired_session_changes_nothing()
    {
        var account = Customer();
        account.StartSession("h1", Now.AddHours(1), Now);

        var (outcome, _) = account.RotateSession("h1", "h2", InTwoWeeks, Now.AddHours(2));

        outcome.Should().Be(SessionRotationOutcome.Expired);
        account.Sessions.Should().ContainSingle(s => s.TokenHash == "h1");
    }

    [Fact]
    public void RotateSession_of_an_unknown_token_is_unknown()
    {
        var account = Customer();

        account.RotateSession("nope", "h2", InTwoWeeks, Now).Outcome.Should().Be(SessionRotationOutcome.Unknown);
    }

    [Fact]
    public void RotateSession_of_a_deactivated_account_is_refused()
    {
        var account = Customer();
        account.StartSession("h1", InTwoWeeks, Now);
        account.Deactivate(Now);

        account.ClearDomainEvents();

        account.RotateSession("h1", "h2", InTwoWeeks, Now).Outcome.Should().Be(SessionRotationOutcome.AccountInactive);
        account.DomainEvents.Should().NotContain(e => e is RefreshTokenReuseDetected);
    }

    [Fact]
    public void Sessions_that_expired_are_pruned_when_a_new_one_starts()
    {
        var account = Customer();
        account.StartSession("old", Now.AddHours(1), Now);

        account.StartSession("new", InTwoWeeks, Now.AddHours(2));

        account.Sessions.Select(s => s.TokenHash).Should().Equal("new");
    }

    [Fact]
    public void EndSession_revokes_the_family_and_ignores_unknown_tokens()
    {
        var account = Customer();
        account.StartSession("h1", InTwoWeeks, Now);
        var (_, successor) = account.RotateSession("h1", "h2", InTwoWeeks, Now);

        account.EndSession("unknown", Now);
        successor!.IsActive(Now).Should().BeTrue();

        account.EndSession("h2", Now);
        successor.IsActive(Now).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_revokes_every_session_and_blocks_new_ones()
    {
        var account = Customer();
        var session = account.StartSession("h1", InTwoWeeks, Now);

        account.Deactivate(Now);
        var start = () => account.StartSession("h2", InTwoWeeks, Now);

        session.IsActive(Now).Should().BeFalse();
        start.Should().Throw<DomainRuleViolationException>().Where(e => e.Code == "account_inactive");
    }
}
