using FluentAssertions;
using OrderCore.Api.Modules.Notifications.Domain.Entities;
using OrderCore.Api.Modules.Notifications.Domain.Enums;
using OrderCore.Api.Modules.Notifications.Domain.Policies;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Notifications;

/// <summary>
/// A queued e-mail is retried with backoff, given up on after its last
/// attempt, and its content is erased once final (password-recovery spec,
/// decision 7).
/// </summary>
public sealed class EmailMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static EmailMessage Queued() =>
        EmailMessage.Queue("jane@example.com", "password-reset", "Redefinição de senha", "<p>link</p>", "link", Now);

    [Fact]
    public void A_queued_email_is_due_at_once()
    {
        var message = Queued();

        message.Status.Should().Be(EmailStatus.Pending);
        message.NextAttemptAt.Should().Be(Now);
        message.Attempts.Should().Be(0);
        message.To.Should().Be("jane@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("jane")]
    [InlineData("jane@")]
    [InlineData("@example.com")]
    [InlineData("jane doe@example.com")]
    public void The_recipient_must_be_an_address(string to)
    {
        var queue = () => EmailMessage.Queue(to, "password-reset", "Assunto", "<p></p>", "", Now);

        queue.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sending_erases_the_content_and_masks_the_recipient()
    {
        var message = Queued();

        message.MarkSent(Now.AddSeconds(1));

        message.Status.Should().Be(EmailStatus.Sent);
        message.SentAt.Should().Be(Now.AddSeconds(1));
        message.Attempts.Should().Be(1);
        message.HtmlBody.Should().BeEmpty();
        message.TextBody.Should().BeEmpty();
        message.To.Should().Be("j***@example.com");
        message.Subject.Should().Be("Redefinição de senha", "the subject says what kind of e-mail it was");
    }

    [Fact]
    public void A_transient_failure_is_retried_after_each_delay_then_given_up_on()
    {
        var message = Queued();
        var policy = EmailRetryPolicy.Default;
        var expectedDelays = new[] { TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30) };

        foreach (var delay in expectedDelays)
        {
            message.RecordFailure("connection refused", policy, Now);
            message.Status.Should().Be(EmailStatus.Pending);
            message.NextAttemptAt.Should().Be(Now + delay);
            message.HtmlBody.Should().NotBeEmpty("it is still to be sent");
        }

        message.RecordFailure("connection refused", policy, Now);

        message.Status.Should().Be(EmailStatus.Failed);
        message.Attempts.Should().Be(5);
        message.FailedAt.Should().Be(Now);
        message.HtmlBody.Should().BeEmpty();
        message.TextBody.Should().BeEmpty();
        message.To.Should().Be("j***@example.com");
        message.LastError.Should().Be("connection refused");
    }

    [Fact]
    public void A_rejected_email_fails_at_once()
    {
        var message = Queued();

        message.Reject("550 mailbox unavailable", Now);

        message.Status.Should().Be(EmailStatus.Failed);
        message.Attempts.Should().Be(1);
        message.ContentErased.Should().BeTrue();
    }

    [Fact]
    public void The_recorded_error_never_keeps_the_address()
    {
        var message = Queued();

        message.Reject("550 5.1.1 <Jane@Example.com>: recipient unknown", Now);

        message.LastError.Should().Be("550 5.1.1 <j***@example.com>: recipient unknown");
    }

    [Fact]
    public void A_final_email_cant_be_sent_again()
    {
        var message = Queued();
        message.MarkSent(Now);

        var sendAgain = () => message.MarkSent(Now);
        var fail = () => message.RecordFailure("x", EmailRetryPolicy.Default, Now);

        sendAgain.Should().Throw<DomainRuleViolationException>().Which.Code.Should().Be("invalid_email_state");
        fail.Should().Throw<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData("jane@example.com", "j***@example.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("not-an-address", "***")]
    public void Masking_keeps_the_first_letter_and_the_domain(string address, string masked)
    {
        EmailMessage.Mask(address).Should().Be(masked);
    }
}
