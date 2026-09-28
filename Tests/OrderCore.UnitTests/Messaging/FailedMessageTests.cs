using FluentAssertions;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Domain.Enums;
using OrderCore.Api.Shared.Domain.Exceptions;
using Xunit;

namespace OrderCore.UnitTests.Messaging;

/// <summary>A failed message is resolved once: replayed or discarded, never both, never twice.</summary>
public sealed class FailedMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static FailedMessage Pending() => FailedMessage.Record(
        Guid.NewGuid(), "tests.thing-happened", 1, "tests.recorder", "{}", null, null, "boom", 5, Now.AddMinutes(-40), Now);

    [Fact]
    public void A_recorded_message_is_pending()
    {
        var message = Pending();

        message.Status.Should().Be(FailedMessageStatus.Pending);
        message.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Replaying_marks_it_replayed()
    {
        var message = Pending();

        message.MarkReplayed(Now);

        message.Status.Should().Be(FailedMessageStatus.Replayed);
        message.ResolvedAt.Should().Be(Now);
    }

    [Fact]
    public void Discarding_marks_it_discarded()
    {
        var message = Pending();

        message.Discard(Now);

        message.Status.Should().Be(FailedMessageStatus.Discarded);
        message.ResolvedAt.Should().Be(Now);
    }

    [Fact]
    public void A_resolved_message_cannot_be_resolved_again()
    {
        var replayed = Pending();
        replayed.MarkReplayed(Now);
        var discarded = Pending();
        discarded.Discard(Now);

        new Action[] { () => replayed.MarkReplayed(Now), () => replayed.Discard(Now), () => discarded.MarkReplayed(Now), discarded.EnsurePending }
            .Should().AllSatisfy(act => act.Should().Throw<DomainRuleViolationException>()
                .Which.Code.Should().Be("invalid_failed_message_state"));
    }

    [Fact]
    public void A_long_error_is_cut_to_fit()
    {
        var message = FailedMessage.Record(
            Guid.NewGuid(), "t", 1, "q", "{}", null, null, new string('x', FailedMessage.MaxErrorLength + 10), 5, Now, Now);

        message.LastError.Should().HaveLength(FailedMessage.MaxErrorLength);
    }
}
